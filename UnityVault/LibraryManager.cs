using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace UnityVault.Editor
{
    /// <summary>
    /// Manages the UnityVault component library - reading, writing, and syncing components.
    /// </summary>
    public static class LibraryManager
    {
        #region Constants

        private const string LIBRARY_PATH = "~/unity-components-library";
        private const string CATALOG_FILE = "catalog.json";

        #endregion

        #region Properties

        public static string LibraryPath
        {
            get
            {
                return LIBRARY_PATH.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
        }

        public static string CatalogPath => Path.Combine(LibraryPath, CATALOG_FILE);

        public static bool LibraryExists => Directory.Exists(LibraryPath);

        public static bool CatalogExists => File.Exists(CatalogPath);

        #endregion

        #region Catalog Operations

        /// <summary>
        /// Load the catalog from the library.
        /// </summary>
        public static CatalogData LoadCatalog()
        {
            if (!CatalogExists)
            {
                Debug.LogWarning($"[LibraryManager] Catalog not found at: {CatalogPath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(CatalogPath);
                return JsonUtility.FromJson<CatalogData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LibraryManager] Failed to load catalog: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Save the catalog to the library.
        /// </summary>
        public static bool SaveCatalog(CatalogData catalog)
        {
            try
            {
                EnsureLibraryExists();
                string json = JsonUtility.ToJson(catalog, true);
                File.WriteAllText(CatalogPath, json);
                Debug.Log($"[LibraryManager] Catalog saved successfully");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LibraryManager] Failed to save catalog: {e.Message}");
                return false;
            }
        }

        #endregion

        #region System Operations

        /// <summary>
        /// Check if a system exists in the library.
        /// </summary>
        public static bool SystemExistsInLibrary(string systemPath)
        {
            string fullPath = Path.Combine(LibraryPath, systemPath);
            return Directory.Exists(fullPath);
        }

        /// <summary>
        /// Get all files for a system.
        /// </summary>
        public static string[] GetSystemFiles(string systemPath)
        {
            string fullPath = Path.Combine(LibraryPath, systemPath);

            if (!Directory.Exists(fullPath))
            {
                return new string[0];
            }

            return Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
        }

        /// <summary>
        /// Import a system from the library to the Unity project.
        /// </summary>
        public static ImportResult ImportSystem(string systemPath, string targetFolder)
        {
            var result = new ImportResult { SystemPath = systemPath };

            try
            {
                string sourcePath = Path.Combine(LibraryPath, systemPath);

                if (!Directory.Exists(sourcePath))
                {
                    result.Success = false;
                    result.Error = $"Source not found: {sourcePath}";
                    return result;
                }

                // Create target folder
                string destPath = Path.Combine(Application.dataPath, targetFolder.Replace("Assets/", ""), Path.GetFileName(systemPath));

                if (!Directory.Exists(destPath))
                {
                    Directory.CreateDirectory(destPath);
                }

                // Copy all files
                var files = Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".json"));

                foreach (var file in files)
                {
                    string relativePath = file.Substring(sourcePath.Length + 1);
                    string destFile = Path.Combine(destPath, relativePath);

                    // Ensure directory exists
                    string destDir = Path.GetDirectoryName(destFile);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destFile, true);
                    result.ImportedFiles.Add(destFile);
                }

                result.Success = true;
                Debug.Log($"[LibraryManager] Imported: {systemPath} ({result.ImportedFiles.Count} files)");
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Error = e.Message;
                Debug.LogError($"[LibraryManager] Import failed: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Add a new system to the library from the Unity project.
        /// </summary>
        public static bool AddSystemToLibrary(string sourcePath, string category, SystemMetadata metadata)
        {
            try
            {
                string systemFolder = Path.Combine(LibraryPath, category, metadata.id);

                // Create folder
                if (!Directory.Exists(systemFolder))
                {
                    Directory.CreateDirectory(systemFolder);
                }

                // Copy files
                string fullSourcePath = Path.Combine(Application.dataPath, sourcePath.Replace("Assets/", ""));

                if (Directory.Exists(fullSourcePath))
                {
                    foreach (var file in Directory.GetFiles(fullSourcePath, "*.cs"))
                    {
                        string destFile = Path.Combine(systemFolder, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }

                // Save metadata
                string metadataPath = Path.Combine(systemFolder, "metadata.json");
                File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));

                Debug.Log($"[LibraryManager] Added system to library: {metadata.name}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LibraryManager] Failed to add system: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove a system from the library.
        /// </summary>
        public static bool RemoveSystemFromLibrary(string systemPath)
        {
            try
            {
                string fullPath = Path.Combine(LibraryPath, systemPath);

                if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, true);
                    Debug.Log($"[LibraryManager] Removed system: {systemPath}");
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LibraryManager] Failed to remove system: {e.Message}");
                return false;
            }
        }

        #endregion

        #region Library Management

        /// <summary>
        /// Ensure the library folder structure exists.
        /// </summary>
        public static void EnsureLibraryExists()
        {
            if (!LibraryExists)
            {
                Directory.CreateDirectory(LibraryPath);
            }

            // Create category folders
            string[] categories = { "Core", "Combat", "Inventory", "AI", "UI", "Audio", "World", "Player", "Skills", "Camera", "VFX" };

            foreach (var category in categories)
            {
                string categoryPath = Path.Combine(LibraryPath, category);
                if (!Directory.Exists(categoryPath))
                {
                    Directory.CreateDirectory(categoryPath);
                }
            }
        }

        /// <summary>
        /// Scan the library and update the catalog with actual installed systems.
        /// </summary>
        public static void SyncCatalog()
        {
            var catalog = LoadCatalog();
            if (catalog == null) return;

            bool changed = false;

            foreach (var system in catalog.systems)
            {
                bool existsInLibrary = SystemExistsInLibrary(system.path);

                if (system.installed != existsInLibrary)
                {
                    system.installed = existsInLibrary;
                    changed = true;
                }
            }

            if (changed)
            {
                SaveCatalog(catalog);
                Debug.Log("[LibraryManager] Catalog synced with library contents");
            }
        }

        /// <summary>
        /// Get library statistics.
        /// </summary>
        public static LibraryStats GetStats()
        {
            var stats = new LibraryStats();
            var catalog = LoadCatalog();

            if (catalog != null)
            {
                stats.TotalSystems = catalog.systems.Length;
                stats.InstalledSystems = catalog.systems.Count(s => s.installed || SystemExistsInLibrary(s.path));
                stats.Categories = catalog.categories?.Length ?? 0;
            }

            if (LibraryExists)
            {
                var files = Directory.GetFiles(LibraryPath, "*.cs", SearchOption.AllDirectories);
                stats.TotalFiles = files.Length;
                stats.TotalSize = files.Sum(f => new FileInfo(f).Length);
            }

            return stats;
        }

        #endregion

        #region Data Classes

        [Serializable]
        public class CatalogData
        {
            public string name;
            public string version;
            public CategoryData[] categories;
            public SystemData[] systems;

            /// <summary>
            /// Find a system by ID.
            /// </summary>
            public SystemData FindSystem(string systemId)
            {
                if (systems == null) return null;
                return systems.FirstOrDefault(s => s.id == systemId);
            }
        }

        [Serializable]
        public class CategoryData
        {
            public string id;
            public string name;
            public string icon;
            public string description;
            public SystemData[] systems;
        }

        [Serializable]
        public class SystemData
        {
            public string id;
            public string name;
            public string category;
            public string priority;
            public string description;
            public string[] gameTypes;
            public string[] dependencies;
            public string[] optionalDeps;
            public string path;
            public string[] files;
            public string[] tags;
            public bool installed;
        }

        [Serializable]
        public class SystemMetadata
        {
            public string id;
            public string name;
            public string version;
            public string author;
            public string category;
            public string description;
            public string[] files;
            public string[] dependencies;
            public string[] tags;
            public string generatedAt;
        }

        public class ImportResult
        {
            public string SystemPath;
            public bool Success;
            public string Error;
            public List<string> ImportedFiles = new List<string>();
        }

        public class LibraryStats
        {
            public int TotalSystems;
            public int InstalledSystems;
            public int Categories;
            public int TotalFiles;
            public long TotalSize;

            public string FormattedSize
            {
                get
                {
                    if (TotalSize < 1024) return $"{TotalSize} B";
                    if (TotalSize < 1024 * 1024) return $"{TotalSize / 1024f:F1} KB";
                    return $"{TotalSize / (1024f * 1024f):F1} MB";
                }
            }
        }

        #endregion
    }
}
