using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static UnityVault.Editor.LibraryManager;

namespace UnityVault.Editor
{
    /// <summary>
    /// Exports selected systems as UnityPackage files.
    /// </summary>
    public static class PackageExporter
    {
        #region Public Methods

        /// <summary>
        /// Export selected systems as a single UnityPackage.
        /// </summary>
        public static ExportResult ExportSystems(List<string> systemIds, string outputPath = null)
        {
            var result = new ExportResult();

            if (systemIds == null || systemIds.Count == 0)
            {
                result.errorMessage = "No systems selected for export";
                return result;
            }

            try
            {
                // Collect all assets to export
                var assetPaths = new List<string>();
                var catalog = LibraryManager.LoadCatalog();

                foreach (var systemId in systemIds)
                {
                    var system = catalog.FindSystem(systemId);
                    if (system == null)
                    {
                        result.warnings.Add($"System not found: {systemId}");
                        continue;
                    }

                    // Get system assets from project
                    var systemAssets = GetSystemAssets(system);
                    if (systemAssets.Count > 0)
                    {
                        assetPaths.AddRange(systemAssets);
                        result.exportedSystems.Add(systemId);
                    }
                    else
                    {
                        result.warnings.Add($"No assets found for: {system.name}");
                    }
                }

                if (assetPaths.Count == 0)
                {
                    result.errorMessage = "No assets to export";
                    return result;
                }

                // Generate output path if not specified
                if (string.IsNullOrEmpty(outputPath))
                {
                    var packageName = GeneratePackageName(result.exportedSystems);
                    outputPath = EditorUtility.SaveFilePanel(
                        "Export UnityPackage",
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        packageName,
                        "unitypackage"
                    );
                }

                if (string.IsNullOrEmpty(outputPath))
                {
                    result.errorMessage = "Export cancelled";
                    return result;
                }

                // Remove duplicates
                assetPaths = assetPaths.Distinct().ToList();

                // Export package
                AssetDatabase.ExportPackage(
                    assetPaths.ToArray(),
                    outputPath,
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
                );

                result.success = true;
                result.outputPath = outputPath;
                result.assetCount = assetPaths.Count;

                Debug.Log($"[PackageExporter] Exported {result.exportedSystems.Count} systems ({result.assetCount} assets) to: {outputPath}");
            }
            catch (Exception ex)
            {
                result.errorMessage = $"Export failed: {ex.Message}";
                Debug.LogError($"[PackageExporter] {result.errorMessage}");
            }

            return result;
        }

        /// <summary>
        /// Export a single system as UnityPackage.
        /// </summary>
        public static ExportResult ExportSystem(string systemId, string outputPath = null)
        {
            return ExportSystems(new List<string> { systemId }, outputPath);
        }

        /// <summary>
        /// Export systems by category.
        /// </summary>
        public static ExportResult ExportCategory(string category, string outputPath = null)
        {
            var catalog = LibraryManager.LoadCatalog();
            var categoryData = catalog.categories.FirstOrDefault(c => c.id == category);

            if (categoryData == null)
            {
                return new ExportResult { errorMessage = $"Category not found: {category}" };
            }

            var systemIds = categoryData.systems.Select(s => s.id).ToList();
            return ExportSystems(systemIds, outputPath);
        }

        /// <summary>
        /// Quick export to desktop with auto-generated name.
        /// </summary>
        public static ExportResult QuickExport(List<string> systemIds)
        {
            var packageName = GeneratePackageName(systemIds);
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var outputPath = Path.Combine(desktopPath, packageName + ".unitypackage");

            // Add number if file exists
            int counter = 1;
            while (File.Exists(outputPath))
            {
                outputPath = Path.Combine(desktopPath, $"{packageName}_{counter}.unitypackage");
                counter++;
            }

            return ExportSystems(systemIds, outputPath);
        }

        #endregion

        #region Private Methods

        private static List<string> GetSystemAssets(SystemData system)
        {
            var assets = new List<string>();

            // Look for system folder in project
            var searchFolders = new[]
            {
                $"Assets/UnityVault/{system.category}/{system.id}",
                $"Assets/UnityVault/{system.category}/{system.name}",
                $"Assets/UnityVault/Generated/{system.category}/{system.id}",
                $"Assets/Scripts/UnityVault/{system.category}"
            };

            foreach (var folder in searchFolders)
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    // Get all assets in folder recursively
                    var guids = AssetDatabase.FindAssets("", new[] { folder });
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (!string.IsNullOrEmpty(path))
                        {
                            assets.Add(path);
                        }
                    }
                    break;
                }
            }

            // If no folder found, search by filename pattern
            if (assets.Count == 0)
            {
                var searchPatterns = new[]
                {
                    system.id,
                    system.name.Replace(" ", ""),
                    system.name
                };

                foreach (var pattern in searchPatterns)
                {
                    var guids = AssetDatabase.FindAssets($"t:Script {pattern}");
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (path.Contains("UnityVault") || path.Contains(pattern))
                        {
                            assets.Add(path);
                        }
                    }

                    if (assets.Count > 0) break;
                }
            }

            return assets;
        }

        private static string GeneratePackageName(List<string> systemIds)
        {
            if (systemIds == null || systemIds.Count == 0)
                return "UnityVault_Export";

            if (systemIds.Count == 1)
                return $"UnityVault_{systemIds[0]}";

            if (systemIds.Count <= 3)
                return $"UnityVault_{string.Join("_", systemIds.Take(3))}";

            return $"UnityVault_{systemIds.Count}Systems_{DateTime.Now:yyyyMMdd}";
        }

        #endregion

        #region Result Classes

        public class ExportResult
        {
            public bool success;
            public string outputPath;
            public string errorMessage;
            public int assetCount;
            public List<string> exportedSystems = new List<string>();
            public List<string> warnings = new List<string>();
        }

        #endregion
    }

    /// <summary>
    /// Editor menu items for quick export.
    /// </summary>
    public static class PackageExporterMenu
    {
        [MenuItem("Assets/UnityVault/Export Selected as Package", false, 100)]
        private static void ExportSelectedAssets()
        {
            var selectedPaths = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (selectedPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("Export", "No assets selected", "OK");
                return;
            }

            var outputPath = EditorUtility.SaveFilePanel(
                "Export UnityPackage",
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "UnityVault_Custom",
                "unitypackage"
            );

            if (string.IsNullOrEmpty(outputPath)) return;

            AssetDatabase.ExportPackage(
                selectedPaths,
                outputPath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
            );

            Debug.Log($"[PackageExporter] Exported {selectedPaths.Length} assets to: {outputPath}");
            EditorUtility.DisplayDialog("Export Complete", $"Exported to:\n{outputPath}", "OK");
        }

        [MenuItem("Assets/UnityVault/Export Selected as Package", true)]
        private static bool ValidateExportSelected()
        {
            return Selection.assetGUIDs.Length > 0;
        }
    }
}
