﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ThunderKit.Common.Package;
using ThunderKit.Core.Manifests;
using UnityEditor;
using UnityEngine;

namespace ThunderKit.Core.Config
{
    public class ManifestUpdater : MonoBehaviour
    {
        static string[] JustPackages = new string[] { "Packages" };
        static string[] PackagesAndAssets = new string[] { "Packages", "Assets" };
        const string MenuPath = "Tools/ThunderKit/Migration/Update Manifest Identity assignments";
        [MenuItem(MenuPath), InitializeOnLoadMethod]
        public static void UpdateAllManifests()
        {
            var remap = new Dictionary<string, string>();
            var allManifests = AssetDatabase.FindAssets($"t:{nameof(Manifest)}", PackagesAndAssets);
            var packageManifests = AssetDatabase.FindAssets($"t:{nameof(Manifest)}", JustPackages);
            var scriptRefRegex = new Regex("(.*?)\\{fileID: (\\d*?), guid: (\\w*?), type: (\\d)\\}");
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in allManifests)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var metaPath = $"{path}.meta";
                    try
                    {
                        var manifest = AssetDatabase.LoadAssetAtPath<Manifest>(path);
                        var dependencyId = $"{manifest.Identity.Author}-{manifest.Identity.Name}";
                        var hash = PackageHelper.GetStringHash(dependencyId);
                        remap.Add(guid, hash);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to identify Manifest GUID for manifest at {path}\r\n{e}");
                    }
                }
                if (!remap.Any(kvp => kvp.Key != kvp.Value)) return;

                var proceed = EditorUtility.DisplayDialog("Update Manifest AssetDatabase",
                    "WARNING: Back up your project before continuing\r\n\r\n" +
                    "ThunderKit 4.0.0 alters management of Manifest files and requires these assets are upgraded to ensure correct functionality.\r\n\r\n" +
                    "This change is not backwards compatible and requires Manifest files are updated.\r\n" +
                    "All Manifest dependency references should be maintained through this upgrade.\r\n" +
                    "If you need to abort this can be manually executed from" +
                    MenuPath,
                    "Continue", "Abort"
                    );
                if (!proceed) return;
                foreach (var guid in allManifests)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var metaPath = $"{path}.meta";
                    try
                    {
                        var dependencyLine = $"  - {{fileID: 11400000, guid: {guid}, type: 2}}";
                        var lines = File.ReadLines(path).ToArray();
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            var match = scriptRefRegex.Match(line);
                            if (!match.Success) continue;

                            var refGuid = $"{match.Groups[3]}";
                            if (remap.ContainsKey(refGuid))
                            {
                                lines[i] = scriptRefRegex.Replace(line, $"{match.Groups[1]}{{fileID: {match.Groups[2]}, guid: {remap[refGuid]}, type: {match.Groups[4]}}}");
                            }
                        }

                        var metaData = PackageHelper.DefaultScriptableObjectMetaData(remap[guid]);
                        if (File.Exists(metaPath)) File.Delete(metaPath);
                        File.WriteAllText(metaPath, metaData);
                        File.WriteAllLines(path, lines);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to update Manifest GUID for manifest at {path}\r\n{e}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }

    }
}