using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnityVault.Editor
{
    public class UnityVaultWindow : EditorWindow
    {
        #region Constants & Colors

        private const string VERSION = "5.1.0";
        private const string LIBRARY_PATH = "~/unity-components-library";
        private const string CACHE_PATH = "~/.unity-vault-cache";
        private const string GITHUB_RAW_BASE = "https://raw.githubusercontent.com/OktyBy/unity-vault-library/main";
        private const string PREFS_FAVORITES = "UnityVault_Favorites";
        private const string PREFS_RECENT = "UnityVault_Recent";
        private const string PREFS_SETTINGS = "UnityVault_Settings";
        private const string GITHUB_GIST_API = "https://api.github.com/gists";

        // Dark theme colors
        private static readonly Color BG_DARK = new Color(0.14f, 0.14f, 0.14f);
        private static readonly Color BG_MEDIUM = new Color(0.18f, 0.18f, 0.18f);
        private static readonly Color BG_LIGHT = new Color(0.22f, 0.22f, 0.22f);
        private static readonly Color BG_CARD = new Color(0.20f, 0.20f, 0.20f);
        private static readonly Color BG_CODE = new Color(0.12f, 0.12f, 0.12f);
        private static readonly Color HEADER_BG = new Color(0.11f, 0.11f, 0.11f);

        private static readonly Color ACCENT_GREEN = new Color(0.30f, 0.75f, 0.40f);
        private static readonly Color ACCENT_BLUE = new Color(0.30f, 0.55f, 0.90f);
        private static readonly Color ACCENT_ORANGE = new Color(0.95f, 0.60f, 0.20f);
        private static readonly Color ACCENT_RED = new Color(0.90f, 0.35f, 0.35f);
        private static readonly Color ACCENT_PURPLE = new Color(0.65f, 0.45f, 0.85f);
        private static readonly Color ACCENT_YELLOW = new Color(0.95f, 0.85f, 0.30f);
        private static readonly Color ACCENT_CYAN = new Color(0.30f, 0.80f, 0.85f);

        private static readonly Color TEXT_WHITE = new Color(0.92f, 0.92f, 0.92f);
        private static readonly Color TEXT_GRAY = new Color(0.55f, 0.55f, 0.55f);
        private static readonly Color TEXT_LIGHT = new Color(0.75f, 0.75f, 0.75f);
        private static readonly Color BORDER = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color HOVER = new Color(0.26f, 0.26f, 0.26f);
        private static readonly Color SELECTED_BG = new Color(0.20f, 0.40f, 0.60f, 0.35f);

        // Code highlighting colors
        private static readonly Color CODE_KEYWORD = new Color(0.6f, 0.4f, 0.8f);
        private static readonly Color CODE_TYPE = new Color(0.4f, 0.8f, 0.8f);
        private static readonly Color CODE_STRING = new Color(0.8f, 0.6f, 0.4f);
        private static readonly Color CODE_COMMENT = new Color(0.4f, 0.6f, 0.4f);

        #endregion

        #region Enums

        private enum Tab { Library, Templates, AI, Settings }
        private enum TemplateSubTab { Templates, Graph, Scenes }

        #endregion

        #region State

        // Core state
        private CatalogData catalog;
        private string libraryPath;
        private string cachePath;
        private bool catalogLoaded = false;
        private bool isDownloading = false;

        // UI state
        private Tab currentTab = Tab.Library;
        private TemplateSubTab templateSubTab = TemplateSubTab.Templates;
        private Vector2 mainScrollPos;
        private Vector2 previewScrollPos;
        private Vector2 codeScrollPos;
        private string searchFilter = "";
        private string selectedTagFilter = "All";
        private int selectedPresetIndex = 0;

        // Selection state
        private Dictionary<string, bool> expandedCategories = new Dictionary<string, bool>();
        private Dictionary<string, bool> selectedSystems = new Dictionary<string, bool>();
        private string previewSystemId = null;
        private string livePreviewCode = null;

        // System options state
        private Dictionary<string, bool> expandedSystemOptions = new Dictionary<string, bool>();
        private Dictionary<string, Dictionary<string, bool>> systemOptionSelections = new Dictionary<string, Dictionary<string, bool>>();
        private string selectedGraphNode = null; // For highlighting dependencies in graph

        // Conflict detection
        private List<ConflictInfo> detectedConflicts = new List<ConflictInfo>();

        // Favorites & Recent
        private HashSet<string> favorites = new HashSet<string>();
        private List<string> recentImports = new List<string>();

        // Settings
        private UserSettings settings = new UserSettings();

        // Progress
        private bool isImporting = false;
        private bool isGenerating = false;
        private float importProgress = 0f;
        private string importStatus = "";

        // MCP Connection state
        private bool isMCPConnected = false;
        private const int MCP_PORT = 7777;

        // Update check state
        private bool isCheckingUpdate = false;
        private string latestVersion = null;
        private bool updateAvailable = false;
        private string lastUpdateCheck = "";

        // Tooltip state
        private SystemData hoveredSystem = null;
        private float tooltipTimer = 0f;
        private const float TOOLTIP_DELAY = 0.5f;
        private double lastMouseMoveTime = 0;

        // Project Analyzer state
        private bool isAnalyzing = false;
        private ProjectAnalysis lastAnalysis = null;
        private bool showAnalysisPopup = false;

        // AI Assistant state
        private List<ChatMessage> chatHistory = new List<ChatMessage>();
        private string chatInput = "";
        private Vector2 chatScrollPos;
        private bool isAIResponding = false;
        private string currentContext = "";

        // Graph view
        private Vector2 graphScrollPos;
        private Vector2 graphOffset = Vector2.zero;
        private float graphZoom = 1f;
        private Dictionary<string, Vector2> nodePositions = new Dictionary<string, Vector2>();

        // Textures
        private Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        // Data
        private readonly string[] presets = { "All", "Action RPG", "Platformer", "FPS", "Survival", "Puzzle" };
        private readonly string[] tagFilters = { "All", "Essential", "Combat", "UI", "AI", "Movement" };

        private readonly Dictionary<string, GameTemplate> gameTemplates = new Dictionary<string, GameTemplate>()
        {
            { "action_rpg", new GameTemplate("Action RPG Starter", "Complete action RPG foundation with combat, inventory, and progression",
                new[] { "health_system", "mana_system", "stat_system", "level_xp", "inventory_system", "equipment_system", "melee_combat", "weapon_system", "save_load", "third_person_controller" }, ACCENT_ORANGE) },
            { "fps_shooter", new GameTemplate("FPS Shooter Kit", "First-person shooter with weapons, health, and audio",
                new[] { "health_system", "first_person_controller", "ranged_combat", "weapon_system", "audio_manager", "camera_shake" }, ACCENT_RED) },
            { "platformer_2d", new GameTemplate("2D Platformer Pack", "Classic platformer mechanics with checkpoints",
                new[] { "health_system", "character_controller_2d", "camera_follow", "checkpoint_system", "trigger_zone" }, ACCENT_BLUE) },
            { "survival", new GameTemplate("Survival Game Core", "Essential survival mechanics with saving",
                new[] { "health_system", "inventory_system", "save_load", "interactable_system", "game_state_manager" }, ACCENT_GREEN) },
            { "rpg_full", new GameTemplate("Full RPG Suite", "Complete RPG with quests, dialogue, and NPCs",
                new[] { "health_system", "mana_system", "stat_system", "level_xp", "inventory_system", "equipment_system", "quest_system", "dialogue_system", "save_load", "ai_state_machine" }, ACCENT_PURPLE) },
            { "mobile_casual", new GameTemplate("Mobile Casual", "Lightweight systems for mobile games",
                new[] { "health_system", "save_load", "audio_manager", "touch_controls" }, ACCENT_YELLOW) }
        };

        private readonly Dictionary<string, SceneTemplate> sceneTemplates = new Dictionary<string, SceneTemplate>()
        {
            { "main_menu", new SceneTemplate("Main Menu", "Complete main menu with buttons and transitions",
                new[] { "Canvas", "EventSystem", "MainMenuManager", "AudioManager" },
                new[] { "game_state_manager", "audio_manager" }, ACCENT_BLUE) },
            { "game_level", new SceneTemplate("Game Level", "Basic game level with player and camera",
                new[] { "Player", "MainCamera", "GameManager", "UI_Canvas" },
                new[] { "health_system", "game_state_manager" }, ACCENT_GREEN) },
            { "combat_arena", new SceneTemplate("Combat Arena", "Arena setup for combat testing",
                new[] { "Player", "EnemySpawner", "CombatManager", "UI_Combat" },
                new[] { "health_system", "melee_combat", "ai_state_machine" }, ACCENT_RED) },
            { "inventory_test", new SceneTemplate("Inventory Test", "Scene for testing inventory UI",
                new[] { "Player", "ItemSpawner", "InventoryUI", "ChestInteractable" },
                new[] { "inventory_system", "interactable_system" }, ACCENT_ORANGE) }
        };

        #endregion

        #region Lifecycle

        [MenuItem("Window/UnityVault %#v")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnityVaultWindow>("UnityVault");
            window.minSize = new Vector2(900, 650);
            window.Show();
        }

        private void OnEnable()
        {
            libraryPath = LIBRARY_PATH.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            cachePath = CACHE_PATH.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            EnsureCacheFolder();
            CreateTextures();
            LoadCatalog();
            LoadUserData();
            DetectConflicts();

            // Auto-check for updates on startup (delayed to allow UI to load)
            if (settings.autoCheckUpdates)
            {
                EditorApplication.delayCall += () => CheckForUpdatesSilent();
            }
        }

        private void CheckForUpdatesSilent()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "UnityVault");
                    string catalogUrl = $"{GITHUB_RAW_BASE}/catalog.json";
                    string catalogJson = client.DownloadString(catalogUrl);

                    var remoteCatalog = JsonUtility.FromJson<CatalogData>(catalogJson);
                    if (remoteCatalog != null && !string.IsNullOrEmpty(remoteCatalog.version))
                    {
                        latestVersion = remoteCatalog.version;
                        updateAvailable = CompareVersions(remoteCatalog.version, VERSION) > 0;
                        lastUpdateCheck = DateTime.Now.ToString("MMM dd, HH:mm");
                        Repaint();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityVault] Auto update check failed: {e.Message}");
            }
        }

        private void EnsureCacheFolder()
        {
            if (!Directory.Exists(cachePath))
                Directory.CreateDirectory(cachePath);
        }

        private void OnDisable()
        {
            SaveUserData();
            DestroyTextures();
        }

        private void CreateTextures()
        {
            textures["bg_dark"] = MakeTex(BG_DARK);
            textures["bg_medium"] = MakeTex(BG_MEDIUM);
            textures["bg_light"] = MakeTex(BG_LIGHT);
            textures["bg_card"] = MakeTex(BG_CARD);
            textures["bg_code"] = MakeTex(BG_CODE);
            textures["header"] = MakeTex(HEADER_BG);
            textures["hover"] = MakeTex(HOVER);
            textures["selected"] = MakeTex(SELECTED_BG);
        }

        private void DestroyTextures()
        {
            foreach (var tex in textures.Values)
                if (tex) DestroyImmediate(tex);
            textures.Clear();
        }

        private Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }

        #endregion

        #region Main OnGUI

        private void OnGUI()
        {
            if (textures.Count == 0) CreateTextures();

            GUI.DrawTexture(new Rect(0, 0, position.width, position.height), textures["bg_dark"]);

            DrawHeader();
            DrawTabBar();

            var contentY = 115;
            var contentHeight = position.height - 115 - (currentTab == Tab.Library ? 95 : 10);

            switch (currentTab)
            {
                case Tab.Library:
                    DrawLibraryTab(contentY, contentHeight);
                    break;
                case Tab.Templates:
                    DrawTemplatesTabWithSubtabs(contentY, contentHeight);
                    break;
                case Tab.AI:
                    DrawAITab(contentY, contentHeight);
                    break;
                case Tab.Settings:
                    DrawSettingsTab(contentY, contentHeight);
                    break;
            }

            if (currentTab == Tab.Library)
            {
                DrawFooter();
            }

            if (isImporting || isGenerating)
            {
                DrawProgressOverlay();
            }

            // Draw tooltip (must be last to appear on top)
            DrawSystemTooltip();

            // Reset hover state at end of frame if mouse not over any system
            if (Event.current.type == EventType.Repaint)
            {
                if (hoveredSystem != null)
                {
                    // Check if still hovering - if not, clear after small delay
                    EditorApplication.delayCall += () => {
                        if (this != null) Repaint();
                    };
                }
            }
        }

        private void DrawSystemTooltip()
        {
            if (hoveredSystem == null) return;
            if (currentTab != Tab.Library) return;

            var mousePos = Event.current.mousePosition;
            float tooltipWidth = 280;
            float tooltipHeight = CalculateTooltipHeight(hoveredSystem);

            // Position tooltip near mouse, but keep it on screen
            float tooltipX = mousePos.x + 15;
            float tooltipY = mousePos.y + 15;

            if (tooltipX + tooltipWidth > position.width - 10)
                tooltipX = mousePos.x - tooltipWidth - 15;
            if (tooltipY + tooltipHeight > position.height - 10)
                tooltipY = mousePos.y - tooltipHeight - 15;

            var tooltipRect = new Rect(tooltipX, tooltipY, tooltipWidth, tooltipHeight);

            // Shadow
            EditorGUI.DrawRect(new Rect(tooltipRect.x + 3, tooltipRect.y + 3, tooltipRect.width, tooltipRect.height),
                new Color(0, 0, 0, 0.4f));

            // Background
            EditorGUI.DrawRect(tooltipRect, new Color(0.12f, 0.12f, 0.12f, 0.98f));
            EditorGUI.DrawRect(new Rect(tooltipRect.x, tooltipRect.y, tooltipRect.width, 2), ACCENT_BLUE);

            float yPos = tooltipRect.y + 10;
            float padding = tooltipRect.x + 12;

            // System name
            GUI.Label(new Rect(padding, yPos, tooltipWidth - 24, 22), hoveredSystem.name, new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            yPos += 24;

            // Category
            GUI.Label(new Rect(padding, yPos, tooltipWidth - 24, 16), $"Category: {hoveredSystem.category}", new GUIStyle() {
                fontSize = 10, normal = { textColor = ACCENT_CYAN }
            });
            yPos += 20;

            // Separator
            EditorGUI.DrawRect(new Rect(padding, yPos, tooltipWidth - 24, 1), BORDER);
            yPos += 8;

            // Description
            var descStyle = new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_LIGHT }, wordWrap = true
            };
            float descHeight = descStyle.CalcHeight(new GUIContent(hoveredSystem.description), tooltipWidth - 24);
            GUI.Label(new Rect(padding, yPos, tooltipWidth - 24, descHeight), hoveredSystem.description, descStyle);
            yPos += descHeight + 10;

            // Dependencies
            if (hoveredSystem.dependencies != null && hoveredSystem.dependencies.Length > 0)
            {
                GUI.Label(new Rect(padding, yPos, tooltipWidth - 24, 16), "Dependencies:", new GUIStyle() {
                    fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_GRAY }
                });
                yPos += 16;

                string deps = string.Join(", ", hoveredSystem.dependencies.Take(5));
                if (hoveredSystem.dependencies.Length > 5)
                    deps += $" +{hoveredSystem.dependencies.Length - 5} more";

                GUI.Label(new Rect(padding + 10, yPos, tooltipWidth - 34, 32), deps, new GUIStyle() {
                    fontSize = 9, normal = { textColor = ACCENT_PURPLE }, wordWrap = true
                });
                yPos += 25;
            }

            // Status info - check actual files, not catalog data
            bool inLibrary = IsSystemInLibrary(hoveredSystem.id);
            bool inProject = IsSystemInProject(hoveredSystem.id);

            string statusText, sourceText;
            Color statusColor;

            if (inProject)
            {
                statusText = "+ Installed in project";
                sourceText = "Ready to use";
                statusColor = ACCENT_GREEN;
            }
            else if (inLibrary)
            {
                statusText = "* Available locally";
                sourceText = "Fast import from cache";
                statusColor = ACCENT_BLUE;
            }
            else
            {
                statusText = "> Cloud only";
                sourceText = "Will download from GitHub";
                statusColor = ACCENT_CYAN;
            }

            EditorGUI.DrawRect(new Rect(padding, yPos, tooltipWidth - 24, 1), BORDER);
            yPos += 8;

            GUI.Label(new Rect(padding, yPos, tooltipWidth - 24, 16), statusText, new GUIStyle() {
                fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = statusColor }
            });
            yPos += 15;

            GUI.Label(new Rect(padding, yPos, tooltipWidth - 24, 14), sourceText, new GUIStyle() {
                fontSize = 9, normal = { textColor = TEXT_GRAY }
            });

            // Reset hovered system at end of frame
            if (Event.current.type == EventType.Repaint)
            {
                hoveredSystem = null;
            }
        }

        private float CalculateTooltipHeight(SystemData system)
        {
            float height = 120; // Base height for name, category, status

            // Description height
            var descStyle = new GUIStyle() { fontSize = 10, wordWrap = true };
            height += descStyle.CalcHeight(new GUIContent(system.description), 256);

            // Dependencies
            if (system.dependencies != null && system.dependencies.Length > 0)
                height += 45;

            return height;
        }

        #endregion

        #region Header & Tabs

        private void DrawHeader()
        {
            var headerRect = new Rect(0, 0, position.width, 60);
            GUI.DrawTexture(headerRect, textures["header"]);

            // Logo
            GUI.Label(new Rect(20, 15, 200, 30), "UnityVault", new GUIStyle() {
                fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = TEXT_WHITE }
            });

            // Task 1: Fixed version badge position (moved right to not overlap)
            DrawBadge(new Rect(155, 20, 50, 20), $"v{VERSION}", ACCENT_BLUE);

            // Conflict warning
            if (detectedConflicts.Count > 0)
            {
                DrawBadge(new Rect(215, 20, 80, 20), $"⚠ {detectedConflicts.Count} conflicts", ACCENT_RED);
            }

            // Search
            var searchRect = new Rect(position.width - 270, 18, 200, 24);
            searchFilter = GUI.TextField(searchRect, searchFilter, GetSearchFieldStyle());

            if (string.IsNullOrEmpty(searchFilter))
            {
                GUI.Label(new Rect(searchRect.x + 8, searchRect.y, 150, 24), "Search systems...",
                    new GUIStyle() { normal = { textColor = TEXT_GRAY }, fontSize = 11, alignment = TextAnchor.MiddleLeft });
            }

            if (GUI.Button(new Rect(position.width - 55, 18, 40, 24), "↻", GetIconButtonStyle()))
            {
                RefreshCatalogFromGitHub();
                DetectConflicts();
            }

            EditorGUI.DrawRect(new Rect(0, 59, position.width, 1), BORDER);
        }

        private void DrawTabBar()
        {
            var tabY = 60;
            var tabHeight = 54;
            GUI.DrawTexture(new Rect(0, tabY, position.width, tabHeight), textures["bg_medium"]);

            float tabWidth = 78;
            float startX = 10;
            float gap = 3;

            DrawTabButton(new Rect(startX, tabY + 12, tabWidth, 32), "Library", Tab.Library, "");
            DrawTabButton(new Rect(startX + (tabWidth + gap) * 1, tabY + 12, tabWidth, 32), "Templates", Tab.Templates, "");
            DrawTabButton(new Rect(startX + (tabWidth + gap) * 2, tabY + 12, tabWidth, 32), "AI", Tab.AI, "");
            DrawTabButton(new Rect(startX + (tabWidth + gap) * 3, tabY + 12, tabWidth, 32), "Settings", Tab.Settings, "");

            // Filters (Library tab only)
            if (currentTab == Tab.Library)
            {
                GUI.Label(new Rect(position.width - 270, tabY + 18, 50, 24), "Filter:",
                    new GUIStyle() { normal = { textColor = TEXT_GRAY }, fontSize = 11 });

                var tagIndex = Array.IndexOf(tagFilters, selectedTagFilter);
                var newTagIndex = EditorGUI.Popup(new Rect(position.width - 220, tabY + 16, 100, 24), tagIndex, tagFilters);
                if (newTagIndex != tagIndex) selectedTagFilter = tagFilters[newTagIndex];

                GUI.Label(new Rect(position.width - 115, tabY + 18, 45, 24), "Preset:",
                    new GUIStyle() { normal = { textColor = TEXT_GRAY }, fontSize = 11 });

                var oldPreset = selectedPresetIndex;
                selectedPresetIndex = EditorGUI.Popup(new Rect(position.width - 70, tabY + 16, 55, 24), selectedPresetIndex, presets);
                if (oldPreset != selectedPresetIndex) ApplyPreset();
            }

            EditorGUI.DrawRect(new Rect(0, tabY + tabHeight - 1, position.width, 1), BORDER);
        }

        private void DrawTabButton(Rect rect, string label, Tab tab, string icon)
        {
            bool isActive = currentTab == tab;

            if (isActive)
                EditorGUI.DrawRect(rect, ACCENT_BLUE);
            else if (rect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rect, HOVER);
                Repaint();
            }

            if (GUI.Button(rect, "", GUIStyle.none))
                currentTab = tab;

            GUI.Label(rect, $"{icon} {label}", new GUIStyle() {
                fontSize = 11, fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = isActive ? Color.white : TEXT_LIGHT }
            });
        }

        #endregion

        #region Library Tab

        private void DrawLibraryTab(float y, float height)
        {
            if (!catalogLoaded)
            {
                DrawNoCatalog(y, height);
                return;
            }

            // Single panel layout - full width list
            float listWidth = position.width - 10;

            // Main list
            var listRect = new Rect(0, y, listWidth, height);
            mainScrollPos = GUI.BeginScrollView(listRect, mainScrollPos,
                new Rect(0, 0, listWidth - 20, CalculateContentHeight()));
            DrawCategories(listWidth - 20);
            GUI.EndScrollView();
        }

        private void DrawCategories(float width)
        {
            if (catalog?.categories == null) return;

            float yPos = 10;
            float padding = 15;
            float cardWidth = width - 30;

            // Conflict warning banner
            if (detectedConflicts.Count > 0 && settings.showConflictWarnings)
            {
                var bannerRect = new Rect(padding, yPos, cardWidth, 45);
                EditorGUI.DrawRect(bannerRect, new Color(ACCENT_RED.r, ACCENT_RED.g, ACCENT_RED.b, 0.2f));
                EditorGUI.DrawRect(new Rect(padding, yPos, 4, 45), ACCENT_RED);

                GUI.Label(new Rect(padding + 15, yPos + 5, cardWidth - 100, 20),
                    $"⚠ {detectedConflicts.Count} potential conflicts detected", new GUIStyle() {
                    fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = ACCENT_RED }
                });
                GUI.Label(new Rect(padding + 15, yPos + 24, cardWidth - 100, 16),
                    "Some systems may conflict with existing code in your project", new GUIStyle() {
                    fontSize = 10, normal = { textColor = TEXT_GRAY }
                });

                if (GUI.Button(new Rect(padding + cardWidth - 80, yPos + 10, 70, 25), "Details", GetMiniButtonStyle()))
                {
                    ShowConflictDetails();
                }

                yPos += 55;
            }

            foreach (var category in catalog.categories)
            {
                var systems = GetFilteredSystems(category.id);
                if (systems.Count == 0) continue;

                bool isExpanded = expandedCategories.ContainsKey(category.id) && expandedCategories[category.id];

                // Task 4: Calculate dynamic card height based on expanded system options
                float cardHeight = 48;
                if (isExpanded)
                {
                    foreach (var system in systems)
                    {
                        cardHeight += GetSystemItemHeight(system);
                    }
                    cardHeight += 10;
                }

                var cardRect = new Rect(padding, yPos, cardWidth, cardHeight);
                GUI.DrawTexture(cardRect, textures["bg_card"]);

                DrawCategoryHeader(padding, yPos, cardWidth, category, systems, isExpanded);

                if (isExpanded)
                {
                    float sysY = yPos + 52;
                    foreach (var system in systems)
                    {
                        float itemHeight = DrawSystemItem(system, padding + 8, sysY, cardWidth - 16);
                        sysY += itemHeight;
                    }
                }

                yPos += cardHeight + 10;
            }
        }

        // Task 4: Calculate height for a system item including options panel
        private float GetSystemItemHeight(SystemData system)
        {
            float baseHeight = 70;

            // Check if options panel is expanded
            if (expandedSystemOptions.ContainsKey(system.id) && expandedSystemOptions[system.id])
            {
                int optionCount = system.options?.Length ?? 0;
                if (optionCount > 0)
                {
                    baseHeight += 25 + (optionCount * 22) + 30; // Header + options + MCP warning space
                }
            }

            return baseHeight;
        }

        private void DrawCategoryHeader(float x, float y, float width, CategoryData category, List<SystemData> systems, bool isExpanded)
        {
            int selectedCount = systems.Count(s => selectedSystems.ContainsKey(s.id) && selectedSystems[s.id]);
            bool hasSelections = selectedCount > 0;

            // Click area for expand/collapse
            if (GUI.Button(new Rect(x, y, width - 40, 48), "", GUIStyle.none))
                expandedCategories[category.id] = !isExpanded;

            // Category name
            GUI.Label(new Rect(x + 15, y + 12, 200, 24), category.name, new GUIStyle() {
                fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            // Small green check icon BEFORE the count (only if has selections)
            float rightAreaX = x + width - 95;
            if (hasSelections)
            {
                var smallCheckRect = new Rect(rightAreaX - 22, y + 15, 16, 16);
                EditorGUI.DrawRect(smallCheckRect, ACCENT_GREEN);
                GUI.Label(new Rect(smallCheckRect.x, smallCheckRect.y - 1, smallCheckRect.width, smallCheckRect.height), "+", new GUIStyle() {
                    fontSize = 10, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
                });

                // Click small check to deselect all
                if (GUI.Button(smallCheckRect, "", GUIStyle.none))
                {
                    foreach (var s in systems) selectedSystems[s.id] = false;
                }
            }

            // Selection count (always visible)
            string countText = hasSelections ? $"{selectedCount}/{systems.Count}" : $"{systems.Count}";
            Color countColor = hasSelections ? ACCENT_GREEN : TEXT_GRAY;
            GUI.Label(new Rect(rightAreaX, y + 14, 50, 20), countText, new GUIStyle() {
                fontSize = 11, normal = { textColor = countColor }, alignment = TextAnchor.MiddleRight
            });

            // Expand/collapse arrow (always visible on the right)
            var arrowRect = new Rect(x + width - 38, y + 13, 26, 22);
            EditorGUI.DrawRect(arrowRect, BG_LIGHT);
            string arrow = isExpanded ? "▲" : "▼";
            GUI.Label(new Rect(arrowRect.x, arrowRect.y - 2, arrowRect.width, arrowRect.height), arrow, new GUIStyle() {
                fontSize = 12, normal = { textColor = TEXT_WHITE }, alignment = TextAnchor.MiddleCenter
            });

            // Click arrow to toggle expand/collapse
            if (GUI.Button(arrowRect, "", GUIStyle.none))
            {
                expandedCategories[category.id] = !isExpanded;
            }
        }

        private float DrawSystemItem(SystemData system, float x, float y, float width)
        {
            if (!selectedSystems.ContainsKey(system.id))
                selectedSystems[system.id] = false;

            bool isSelected = selectedSystems[system.id];
            bool isFavorite = favorites.Contains(system.id);
            float itemHeight = GetSystemItemHeight(system);
            bool isHovered = new Rect(x, y, width, itemHeight).Contains(Event.current.mousePosition);
            bool hasConflict = detectedConflicts.Any(c => c.systemId == system.id);
            bool hasOptions = system.options != null && system.options.Length > 0;
            bool optionsExpanded = expandedSystemOptions.ContainsKey(system.id) && expandedSystemOptions[system.id];

            // Background
            var bgRect = new Rect(x, y, width, itemHeight);
            if (hasConflict)
                EditorGUI.DrawRect(bgRect, new Color(ACCENT_RED.r, ACCENT_RED.g, ACCENT_RED.b, 0.1f));
            else if (isSelected)
                EditorGUI.DrawRect(bgRect, new Color(ACCENT_GREEN.r, ACCENT_GREEN.g, ACCENT_GREEN.b, 0.15f));
            else if (isHovered)
            {
                GUI.DrawTexture(bgRect, textures["hover"]);
                Repaint();
            }

            // Track hover for tooltip
            if (isHovered && Event.current.type == EventType.Repaint)
            {
                hoveredSystem = system;
            }

            // Conflict indicator
            if (hasConflict)
            {
                EditorGUI.DrawRect(new Rect(x, y, 3, itemHeight), ACCENT_RED);
            }

            // Checkbox
            if (GUI.Button(new Rect(x + 10, y + 20, 24, 24), "", GUIStyle.none))
            {
                selectedSystems[system.id] = !isSelected;
                if (settings.autoSelectDependencies && selectedSystems[system.id])
                    SelectDependencies(system.id);
                DetectConflicts();
            }

            var checkColor = isSelected ? ACCENT_GREEN : BG_LIGHT;
            EditorGUI.DrawRect(new Rect(x + 10, y + 20, 24, 24), checkColor);
            if (isSelected)
                GUI.Label(new Rect(x + 10, y + 18, 24, 24), "+", new GUIStyle() {
                    fontSize = 16, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
                });

            // System name
            GUI.Label(new Rect(x + 45, y + 12, width - 180, 24), system.name, new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            // Tags
            float tagX = x + 45;
            float tagY = y + 38;

            if (system.priority == "P0")
            {
                DrawBadge(new Rect(tagX, tagY, 55, 16), "Essential", ACCENT_ORANGE);
                tagX += 60;
            }

            if (hasConflict)
            {
                DrawBadge(new Rect(tagX, tagY, 50, 16), "Conflict", ACCENT_RED);
                tagX += 55;
            }

            if (system.dependencies != null && system.dependencies.Length > 0)
            {
                DrawBadge(new Rect(tagX, tagY, 55, 16), $"+{system.dependencies.Length} deps", ACCENT_PURPLE);
                tagX += 60;
            }

            // Task 4: Options badge (clickable to expand)
            if (hasOptions)
            {
                var optionsBadgeRect = new Rect(tagX, tagY, 55, 16);
                DrawBadge(optionsBadgeRect, optionsExpanded ? "▲ Options" : "▼ Options", ACCENT_CYAN);
                if (GUI.Button(optionsBadgeRect, "", GUIStyle.none))
                {
                    expandedSystemOptions[system.id] = !optionsExpanded;
                    Repaint();
                }
                tagX += 60;
            }

            // Description
            var desc = system.description.Length > 35 ? system.description.Substring(0, 32) + "..." : system.description;
            GUI.Label(new Rect(tagX + 5, tagY - 2, width - tagX - 100, 20), desc, new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });

            // Right side
            float rightX = x + width - 90;

            // Favorite
            if (GUI.Button(new Rect(rightX, y + 20, 24, 24), "", GUIStyle.none))
            {
                if (isFavorite) favorites.Remove(system.id);
                else favorites.Add(system.id);
            }
            GUI.Label(new Rect(rightX, y + 18, 24, 24), isFavorite ? "*" : "o", new GUIStyle() {
                fontSize = 16, normal = { textColor = isFavorite ? ACCENT_YELLOW : TEXT_GRAY }, alignment = TextAnchor.MiddleCenter
            });

            // Status - check actual files, not catalog data
            bool inLibrary = IsSystemInLibrary(system.id);
            bool inProject = IsSystemInProject(system.id);

            string statusIcon, statusText;
            Color statusColor;

            if (inProject)
            {
                statusIcon = "+"; statusText = "Installed"; statusColor = ACCENT_GREEN;
            }
            else if (inLibrary)
            {
                statusIcon = "*"; statusText = "Local"; statusColor = ACCENT_BLUE;
            }
            else
            {
                statusIcon = ">"; statusText = "Cloud"; statusColor = ACCENT_CYAN;
            }

            GUI.Label(new Rect(rightX + 28, y + 22, 60, 20), $"{statusIcon} {statusText}", new GUIStyle() {
                fontSize = 10, normal = { textColor = statusColor }, alignment = TextAnchor.MiddleLeft
            });

            // Task 4: Draw options panel if expanded
            if (hasOptions && optionsExpanded)
            {
                DrawSystemOptionsPanel(system, x + 20, y + 62, width - 30);
            }

            return itemHeight;
        }

        // Task 4: Draw expandable options panel for a system
        private void DrawSystemOptionsPanel(SystemData system, float x, float y, float width)
        {
            // Background
            EditorGUI.DrawRect(new Rect(x, y, width, GetSystemItemHeight(system) - 70), new Color(0.1f, 0.1f, 0.1f, 0.5f));

            float optY = y + 5;

            // Header with MCP status
            GUI.Label(new Rect(x + 10, optY, 150, 18), "System Options", new GUIStyle() {
                fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });

            // Check MCP connection
            CheckMCPConnection();

            // MCP status indicator
            Color mcpColor = isMCPConnected ? ACCENT_GREEN : ACCENT_RED;
            string mcpText = isMCPConnected ? "* MCP Connected" : "! MCP Not Connected";
            GUI.Label(new Rect(x + width - 140, optY, 130, 18), mcpText, new GUIStyle() {
                fontSize = 9, normal = { textColor = mcpColor }, alignment = TextAnchor.MiddleRight
            });

            optY += 22;

            // Warning if MCP not connected
            if (!isMCPConnected)
            {
                EditorGUI.DrawRect(new Rect(x + 10, optY, width - 20, 24), new Color(ACCENT_RED.r, ACCENT_RED.g, ACCENT_RED.b, 0.2f));
                GUI.Label(new Rect(x + 15, optY + 2, width - 30, 20),
                    "Options require MCP Bridge. Changes will be saved but not applied automatically.", new GUIStyle() {
                    fontSize = 9, normal = { textColor = ACCENT_ORANGE }, wordWrap = true
                });
                optY += 28;
            }

            // Draw each option
            var options = GetSystemOptions(system.id);

            foreach (var opt in system.options)
            {
                bool isEnabled = options.ContainsKey(opt.id) && options[opt.id];

                // Option checkbox
                var checkRect = new Rect(x + 10, optY + 2, 16, 16);
                EditorGUI.DrawRect(checkRect, isEnabled ? ACCENT_CYAN : BG_LIGHT);
                if (isEnabled)
                    GUI.Label(new Rect(checkRect.x, checkRect.y - 2, 16, 16), "+", new GUIStyle() {
                        fontSize = 12, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
                    });

                if (GUI.Button(checkRect, "", GUIStyle.none))
                {
                    options[opt.id] = !isEnabled;
                    Repaint();
                }

                // Option label
                GUI.Label(new Rect(x + 32, optY, 150, 18), opt.name, new GUIStyle() {
                    fontSize = 10, normal = { textColor = TEXT_WHITE }
                });

                // Option description
                if (!string.IsNullOrEmpty(opt.description))
                {
                    GUI.Label(new Rect(x + 180, optY, width - 200, 18), opt.description, new GUIStyle() {
                        fontSize = 9, normal = { textColor = TEXT_GRAY }
                    });
                }

                optY += 22;
            }
        }

        #endregion

        #region Preview Panel

        private void DrawPreviewPanel(Rect rect)
        {
            GUI.DrawTexture(rect, textures["bg_medium"]);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), BORDER);

            var system = catalog?.systems?.FirstOrDefault(s => s.id == previewSystemId);
            if (system == null) return;

            float padding = 12;
            float y = rect.y + padding;

            // Close button
            if (GUI.Button(new Rect(rect.x + rect.width - 25, y, 18, 18), "x", GetMiniButtonStyle()))
            {
                previewSystemId = null;
                livePreviewCode = null;
                return;
            }

            // System name
            GUI.Label(new Rect(rect.x + padding, y, rect.width - 45, 24), system.name, new GUIStyle() {
                fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            y += 30;

            // Tags row
            float tagX = rect.x + padding;
            DrawBadge(new Rect(tagX, y, 50, 16), system.category, ACCENT_BLUE);
            tagX += 55;
            if (system.priority == "P0")
            {
                DrawBadge(new Rect(tagX, y, 50, 16), "Essential", ACCENT_ORANGE);
            }
            y += 25;

            // Description
            GUI.Label(new Rect(rect.x + padding, y, rect.width - 24, 50), system.description, new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_LIGHT }, wordWrap = true
            });
            y += 55;

            EditorGUI.DrawRect(new Rect(rect.x + padding, y, rect.width - 24, 1), BORDER);
            y += 12;

            // Dependencies
            if (system.dependencies != null && system.dependencies.Length > 0)
            {
                GUI.Label(new Rect(rect.x + padding, y, 100, 18), "Dependencies:", new GUIStyle() {
                    fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_GRAY }
                });
                y += 20;

                foreach (var depId in system.dependencies)
                {
                    var dep = catalog?.systems?.FirstOrDefault(s => s.id == depId);
                    string depName = dep?.name ?? depId;
                    bool depSelected = selectedSystems.ContainsKey(depId) && selectedSystems[depId];

                    GUI.Label(new Rect(rect.x + padding + 8, y, rect.width - 35, 16), $"• {depName}", new GUIStyle() {
                        fontSize = 10, normal = { textColor = depSelected ? ACCENT_GREEN : ACCENT_ORANGE }
                    });
                    y += 17;
                }
                y += 8;
            }

            // Files
            if (system.files != null && system.files.Length > 0)
            {
                GUI.Label(new Rect(rect.x + padding, y, 100, 18), "Files:", new GUIStyle() {
                    fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_GRAY }
                });
                y += 20;

                foreach (var file in system.files.Take(5))
                {
                    GUI.Label(new Rect(rect.x + padding + 8, y, rect.width - 35, 16), $"📄 {file}", new GUIStyle() {
                        fontSize = 10, normal = { textColor = TEXT_LIGHT }
                    });
                    y += 17;
                }
                y += 8;
            }

            // Action buttons
            float btnY = rect.y + rect.height - 80;

            bool isSelected = selectedSystems.ContainsKey(system.id) && selectedSystems[system.id];
            if (DrawButton(new Rect(rect.x + padding, btnY, rect.width - 24, 28),
                isSelected ? "+ Selected" : "Select", isSelected ? ACCENT_GREEN : ACCENT_BLUE, true))
            {
                selectedSystems[system.id] = !isSelected;
                if (settings.autoSelectDependencies && selectedSystems[system.id])
                    SelectDependencies(system.id);
            }

            // View Code button - Task 3: Fixed toggle behavior
            if (DrawButton(new Rect(rect.x + padding, btnY + 35, rect.width - 24, 28),
                livePreviewCode != null ? "Hide Code" : "View Code", ACCENT_PURPLE, true))
            {
                if (livePreviewCode != null)
                {
                    livePreviewCode = null;
                }
                else
                {
                    LoadLivePreview(system);
                }
                Repaint(); // Force UI update
            }
        }

        #endregion

        #region Live Code Preview

        private void DrawCodePreview(Rect rect)
        {
            GUI.DrawTexture(rect, textures["bg_code"]);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), BORDER);

            float padding = 10;

            // Header
            GUI.Label(new Rect(rect.x + padding, rect.y + 8, rect.width - 50, 20), "Code Preview", new GUIStyle() {
                fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            // Task 3: Fixed close button - use proper state management
            if (GUI.Button(new Rect(rect.x + rect.width - 25, rect.y + 8, 18, 18), "x", GetMiniButtonStyle()))
            {
                livePreviewCode = null;
                Repaint(); // Force UI update
                return;
            }

            // Code area
            var codeRect = new Rect(rect.x + padding, rect.y + 35, rect.width - 20, rect.height - 45);
            GUI.DrawTexture(codeRect, textures["bg_dark"]);

            // Scrollable code
            var codeContentHeight = Mathf.Max(rect.height - 55, livePreviewCode.Split('\n').Length * 15 + 20);
            codeScrollPos = GUI.BeginScrollView(
                new Rect(codeRect.x, codeRect.y, codeRect.width, codeRect.height),
                codeScrollPos,
                new Rect(0, 0, codeRect.width - 20, codeContentHeight));

            // Draw code with syntax highlighting
            DrawHighlightedCode(new Rect(5, 5, codeRect.width - 30, codeContentHeight), livePreviewCode);

            GUI.EndScrollView();
        }

        private void DrawHighlightedCode(Rect rect, string code)
        {
            var lines = code.Split('\n');
            float y = rect.y;
            float lineHeight = 15;

            var codeStyle = new GUIStyle() {
                fontSize = 11,
                normal = { textColor = TEXT_LIGHT },
                richText = true,
                wordWrap = false
            };

            var lineNumStyle = new GUIStyle() {
                fontSize = 10,
                normal = { textColor = TEXT_GRAY },
                alignment = TextAnchor.MiddleRight
            };

            for (int i = 0; i < lines.Length; i++)
            {
                // Line number
                GUI.Label(new Rect(rect.x, y, 30, lineHeight), $"{i + 1}", lineNumStyle);

                // Highlighted code
                string highlighted = HighlightSyntax(lines[i]);
                GUI.Label(new Rect(rect.x + 40, y, rect.width - 45, lineHeight), highlighted, codeStyle);

                y += lineHeight;
            }
        }

        private string HighlightSyntax(string line)
        {
            // Simple syntax highlighting
            var keywords = new[] { "public", "private", "protected", "class", "void", "int", "float", "bool", "string", "if", "else", "for", "foreach", "while", "return", "new", "null", "true", "false", "using", "namespace", "static", "readonly", "const", "override", "virtual", "abstract", "sealed", "partial", "get", "set", "this", "base", "event", "delegate", "async", "await" };
            var types = new[] { "MonoBehaviour", "GameObject", "Transform", "Vector3", "Vector2", "Quaternion", "Color", "Action", "Func", "List", "Dictionary", "HashSet", "IEnumerator", "Coroutine", "SerializeField", "Header", "Tooltip", "Range" };

            string result = line;

            // Comments (simple detection)
            if (line.TrimStart().StartsWith("//"))
            {
                return $"<color=#{ColorToHex(CODE_COMMENT)}>{line}</color>";
            }

            // Strings
            result = Regex.Replace(result, "\"[^\"]*\"", m => $"<color=#{ColorToHex(CODE_STRING)}>{m.Value}</color>");

            // Keywords
            foreach (var kw in keywords)
            {
                result = Regex.Replace(result, $@"\b{kw}\b", $"<color=#{ColorToHex(CODE_KEYWORD)}>{kw}</color>");
            }

            // Types
            foreach (var t in types)
            {
                result = Regex.Replace(result, $@"\b{t}\b", $"<color=#{ColorToHex(CODE_TYPE)}>{t}</color>");
            }

            return result;
        }

        private string ColorToHex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        private void LoadLivePreview(SystemData system)
        {
            if (system == null)
            {
                livePreviewCode = "// No system selected";
                return;
            }

            string sourcePath = Path.Combine(libraryPath, system.path);

            if (!Directory.Exists(sourcePath))
            {
                livePreviewCode = $"// System not found in library\n// Path: {sourcePath}\n// Will be generated on import";
                return;
            }

            var csFiles = Directory.GetFiles(sourcePath, "*.cs");
            if (csFiles.Length > 0)
            {
                // Load first/main file - case insensitive matching
                string searchName = system.id.Replace("_", "").ToLower();
                var mainFile = csFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).ToLower().Contains(searchName) ||
                    Path.GetFileNameWithoutExtension(f).ToLower().Replace("_", "") == searchName
                ) ?? csFiles[0];

                try
                {
                    livePreviewCode = File.ReadAllText(mainFile);

                    // Truncate if too long
                    if (livePreviewCode.Length > 15000)
                    {
                        livePreviewCode = livePreviewCode.Substring(0, 15000) + "\n\n// ... truncated ...";
                    }
                }
                catch (Exception e)
                {
                    livePreviewCode = $"// Error reading file: {e.Message}";
                }
            }
            else
            {
                livePreviewCode = $"// No source files found in:\n// {sourcePath}";
            }
        }

        #endregion

        #region Dependency Graph Tab

        private void DrawGraphTab(float y, float height)
        {
            if (!catalogLoaded)
            {
                DrawNoCatalog(y, height);
                return;
            }

            var graphRect = new Rect(0, y, position.width, height);
            GUI.DrawTexture(graphRect, textures["bg_dark"]);

            // Controls
            GUI.Label(new Rect(15, y + 10, 200, 20), "Dependency Graph", new GUIStyle() {
                fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            GUI.Label(new Rect(15, y + 32, 100, 18), "Zoom:", new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });
            graphZoom = GUI.HorizontalSlider(new Rect(55, y + 35, 100, 18), graphZoom, 0.5f, 2f);

            if (GUI.Button(new Rect(170, y + 30, 80, 22), "Reset View", GetMiniButtonStyle()))
            {
                graphOffset = Vector2.zero;
                graphZoom = 1f;
                LayoutGraphNodes();
            }

            if (GUI.Button(new Rect(260, y + 30, 100, 22), "Auto Layout", GetMiniButtonStyle()))
            {
                LayoutGraphNodes();
            }

            // Graph area
            var graphAreaRect = new Rect(10, y + 60, position.width - 20, height - 70);
            GUI.DrawTexture(graphAreaRect, textures["bg_medium"]);

            // Handle drag
            if (Event.current.type == EventType.MouseDrag && graphAreaRect.Contains(Event.current.mousePosition))
            {
                graphOffset += Event.current.delta;
                Repaint();
            }

            // Draw connections and nodes
            GUI.BeginClip(graphAreaRect);
            DrawGraphConnections(graphAreaRect);
            DrawGraphNodes(graphAreaRect);
            GUI.EndClip();

            // Legend - Task 2: Added Focused badge for graph selection
            float legendY = y + height - 40;
            GUI.Label(new Rect(15, legendY, 50, 18), "Legend:", new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });
            DrawBadge(new Rect(70, legendY, 50, 16), "Focused", ACCENT_CYAN);
            DrawBadge(new Rect(125, legendY, 55, 16), "Selected", ACCENT_GREEN);
            DrawBadge(new Rect(185, legendY, 55, 16), "Essential", ACCENT_ORANGE);
            DrawBadge(new Rect(245, legendY, 65, 16), "Has Deps", ACCENT_PURPLE);

            // Hint for focus
            GUI.Label(new Rect(320, legendY, 300, 16), "Click node to highlight dependencies • Double-click to select", new GUIStyle() {
                fontSize = 9, normal = { textColor = TEXT_GRAY }
            });
        }

        private void LayoutGraphNodes()
        {
            if (catalog?.systems == null) return;

            nodePositions.Clear();
            var selected = selectedSystems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

            // If nothing selected, show essential systems
            var systemsToShow = selected.Count > 0 ? selected :
                catalog.systems.Where(s => s.priority == "P0").Select(s => s.id).ToList();

            // Add dependencies
            var allSystems = new HashSet<string>(systemsToShow);
            foreach (var sysId in systemsToShow.ToList())
            {
                var system = catalog.systems.FirstOrDefault(s => s.id == sysId);
                if (system?.dependencies != null)
                {
                    foreach (var dep in system.dependencies)
                        allSystems.Add(dep);
                }
            }

            // Layout in grid
            int cols = Mathf.CeilToInt(Mathf.Sqrt(allSystems.Count));
            int i = 0;
            foreach (var sysId in allSystems)
            {
                int row = i / cols;
                int col = i % cols;
                nodePositions[sysId] = new Vector2(col * 180 + 100, row * 100 + 50);
                i++;
            }
        }

        private void DrawGraphConnections(Rect area)
        {
            if (catalog?.systems == null) return;

            // Task 2: First pass - draw non-highlighted connections
            foreach (var sysId in nodePositions.Keys)
            {
                var system = catalog.systems.FirstOrDefault(s => s.id == sysId);
                if (system?.dependencies == null) continue;

                // Skip if this is the selected node (will draw highlighted later)
                if (sysId == selectedGraphNode) continue;

                Vector2 fromPos = (nodePositions[sysId] + graphOffset) * graphZoom;
                fromPos.x += 70;
                fromPos.y += 20;

                foreach (var depId in system.dependencies)
                {
                    if (!nodePositions.ContainsKey(depId)) continue;
                    // Skip if this connection goes to a dependency of selected node
                    if (selectedGraphNode != null)
                    {
                        var selectedSystem = catalog.systems.FirstOrDefault(s => s.id == selectedGraphNode);
                        if (selectedSystem?.dependencies != null && selectedSystem.dependencies.Contains(depId))
                            continue;
                    }

                    Vector2 toPos = (nodePositions[depId] + graphOffset) * graphZoom;
                    toPos.x += 70;
                    toPos.y += 20;

                    // Draw bezier curve with default color
                    DrawConnection(fromPos, toPos, new Color(ACCENT_PURPLE.r, ACCENT_PURPLE.g, ACCENT_PURPLE.b, 0.4f));
                }
            }

            // Task 2: Second pass - draw highlighted connections for selected node
            if (selectedGraphNode != null && nodePositions.ContainsKey(selectedGraphNode))
            {
                var selectedSystem = catalog.systems.FirstOrDefault(s => s.id == selectedGraphNode);
                if (selectedSystem?.dependencies != null)
                {
                    Vector2 fromPos = (nodePositions[selectedGraphNode] + graphOffset) * graphZoom;
                    fromPos.x += 70;
                    fromPos.y += 20;

                    foreach (var depId in selectedSystem.dependencies)
                    {
                        if (!nodePositions.ContainsKey(depId)) continue;

                        Vector2 toPos = (nodePositions[depId] + graphOffset) * graphZoom;
                        toPos.x += 70;
                        toPos.y += 20;

                        // Draw highlighted connection with bright cyan
                        DrawConnection(fromPos, toPos, ACCENT_CYAN, 3f);
                    }
                }
            }
        }

        private void DrawConnection(Vector2 from, Vector2 to, Color color, float lineWidth = 2f)
        {
            Handles.BeginGUI();
            Handles.color = color;

            Vector2 startTan = from + Vector2.right * 50;
            Vector2 endTan = to + Vector2.left * 50;

            Handles.DrawBezier(from, to, startTan, endTan, color, null, lineWidth);

            // Arrow
            Vector2 dir = (to - endTan).normalized;
            Vector2 arrowPos = to - dir * 5;
            Handles.DrawSolidDisc(arrowPos, Vector3.forward, lineWidth + 2);

            Handles.EndGUI();
        }

        private void DrawGraphNodes(Rect area)
        {
            foreach (var kvp in nodePositions)
            {
                var system = catalog?.systems?.FirstOrDefault(s => s.id == kvp.Key);
                if (system == null) continue;

                Vector2 pos = (kvp.Value + graphOffset) * graphZoom;
                var nodeRect = new Rect(pos.x, pos.y, 140 * graphZoom, 40 * graphZoom);

                bool isSelected = selectedSystems.ContainsKey(system.id) && selectedSystems[system.id];
                bool isEssential = system.priority == "P0";
                bool hasDeps = system.dependencies != null && system.dependencies.Length > 0;
                bool isGraphSelected = selectedGraphNode == system.id; // Task 2: Track graph selection

                Color nodeColor = isGraphSelected ? ACCENT_CYAN : (isSelected ? ACCENT_GREEN : (isEssential ? ACCENT_ORANGE : (hasDeps ? ACCENT_PURPLE : BG_LIGHT)));

                // Task 2: Highlight selected node with thicker border
                if (isGraphSelected)
                {
                    EditorGUI.DrawRect(new Rect(nodeRect.x - 2, nodeRect.y - 2, nodeRect.width + 4, nodeRect.height + 4), ACCENT_CYAN);
                }

                EditorGUI.DrawRect(nodeRect, BG_CARD);
                EditorGUI.DrawRect(new Rect(nodeRect.x, nodeRect.y, nodeRect.width, 3), nodeColor);

                // Node click - Task 2: Set graph selection on single click, toggle selection on double click
                if (GUI.Button(nodeRect, "", GUIStyle.none))
                {
                    if (Event.current.clickCount == 2)
                    {
                        // Double click: toggle checkbox selection
                        selectedSystems[system.id] = !isSelected;
                        if (settings.autoSelectDependencies && selectedSystems[system.id])
                            SelectDependencies(system.id);
                    }
                    else
                    {
                        // Single click: select for dependency highlighting
                        selectedGraphNode = selectedGraphNode == system.id ? null : system.id;
                    }
                    Repaint();
                }

                // Label
                GUI.Label(new Rect(nodeRect.x + 5, nodeRect.y + 8, nodeRect.width - 10, 24), system.name, new GUIStyle() {
                    fontSize = Mathf.RoundToInt(11 * graphZoom),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = TEXT_WHITE },
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                });
            }
        }

        #endregion

        #region Scene Templates Tab

        private void DrawScenesTab(float y, float height)
        {
            mainScrollPos = GUI.BeginScrollView(new Rect(0, y, position.width, height), mainScrollPos,
                new Rect(0, 0, position.width - 20, sceneTemplates.Count * 130 + 200));

            float padding = 20;
            float yPos = 20;

            // Header
            GUI.Label(new Rect(padding, yPos, 400, 30), "Scene Templates", new GUIStyle() {
                fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            GUI.Label(new Rect(padding, yPos + 28, 500, 20), "Create pre-configured scenes with Auto-Wiring", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            yPos += 65;

            // Scene template cards
            foreach (var kvp in sceneTemplates)
            {
                DrawSceneTemplateCard(padding, yPos, position.width - 60, kvp.Key, kvp.Value);
                yPos += 120;
            }

            // Custom scene section
            yPos += 20;
            EditorGUI.DrawRect(new Rect(padding, yPos, position.width - 60, 1), BORDER);
            yPos += 20;

            GUI.Label(new Rect(padding, yPos, 300, 25), "Quick Auto-Wire", new GUIStyle() {
                fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            yPos += 30;

            GUI.Label(new Rect(padding, yPos, 500, 40),
                "Auto-wire selected systems to the current scene.\nThis will create GameObjects and connect components automatically.", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }, wordWrap = true
            });
            yPos += 50;

            var selectedCount = selectedSystems.Count(kvp => kvp.Value);
            if (DrawButton(new Rect(padding, yPos, 200, 35), $"Auto-Wire {selectedCount} Systems", ACCENT_CYAN, selectedCount > 0))
            {
                AutoWireSelectedSystems();
            }

            GUI.EndScrollView();
        }

        private void DrawSceneTemplateCard(float x, float y, float width, string id, SceneTemplate template)
        {
            var cardRect = new Rect(x, y, width, 105);
            GUI.DrawTexture(cardRect, textures["bg_card"]);

            EditorGUI.DrawRect(new Rect(x, y, 4, 105), template.color);

            GUI.Label(new Rect(x + 20, y + 15, width - 200, 24), template.name, new GUIStyle() {
                fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            GUI.Label(new Rect(x + 20, y + 40, width - 200, 20), template.description, new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });

            // Objects preview
            string objectsText = string.Join(", ", template.objects.Take(3));
            if (template.objects.Length > 3) objectsText += $" +{template.objects.Length - 3}";
            GUI.Label(new Rect(x + 20, y + 62, width - 200, 18), $"Objects: {objectsText}", new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_LIGHT }
            });

            // Required systems badge
            DrawBadge(new Rect(x + 20, y + 82, 90, 18), $"{template.requiredSystems.Length} systems", template.color);

            // Create button
            if (DrawButton(new Rect(x + width - 140, y + 35, 120, 35), "Create Scene", template.color, true))
            {
                CreateSceneFromTemplate(template);
            }
        }

        private void CreateSceneFromTemplate(SceneTemplate template)
        {
            // Ask for scene name
            string sceneName = template.name.Replace(" ", "_");

            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Create objects from template
            foreach (var objName in template.objects)
            {
                var go = new GameObject(objName);

                // Auto-setup based on name
                if (objName.Contains("Camera"))
                {
                    go.AddComponent<Camera>();
                    go.AddComponent<AudioListener>();
                    go.transform.position = new Vector3(0, 5, -10);
                }
                else if (objName.Contains("Canvas") || objName.Contains("UI"))
                {
                    go.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                    go.AddComponent<UnityEngine.UI.CanvasScaler>();
                    go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
                else if (objName.Contains("EventSystem"))
                {
                    go.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                }
                else if (objName.Contains("Player"))
                {
                    // Basic player setup
                    go.AddComponent<CharacterController>();
                    go.transform.position = Vector3.zero;
                }
                else if (objName.Contains("Light"))
                {
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Directional;
                    go.transform.rotation = Quaternion.Euler(50, -30, 0);
                }
            }

            // Add directional light if not present
            if (!template.objects.Any(o => o.Contains("Light")))
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // Save scene
            string scenePath = EditorUtility.SaveFilePanel("Save Scene", "Assets/Scenes", sceneName, "unity");
            if (!string.IsNullOrEmpty(scenePath))
            {
                scenePath = "Assets" + scenePath.Substring(Application.dataPath.Length);
                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.Refresh();
            }

            // Select required systems
            foreach (var sysId in template.requiredSystems)
            {
                selectedSystems[sysId] = true;
            }

            EditorUtility.DisplayDialog("Scene Created",
                $"Scene '{template.name}' created!\n\n{template.requiredSystems.Length} systems selected for import.", "OK");
        }

        private void AutoWireSelectedSystems()
        {
            var selected = selectedSystems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            if (selected.Count == 0) return;

            // Create GameManager if not exists
            var gameManager = GameObject.Find("GameManager");
            if (gameManager == null)
            {
                gameManager = new GameObject("GameManager");
            }

            int wiredCount = 0;

            foreach (var sysId in selected)
            {
                var system = catalog?.systems?.FirstOrDefault(s => s.id == sysId);
                if (system == null) continue;

                // Try to find or add component
                string mainClass = system.name.Replace(" ", "");
                var type = GetTypeByName(mainClass);

                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    // Check if already exists
                    var existing = gameManager.GetComponent(type);
                    if (existing == null)
                    {
                        gameManager.AddComponent(type);
                        wiredCount++;
                    }
                }
            }

            EditorUtility.DisplayDialog("Auto-Wire Complete",
                $"Added {wiredCount} components to GameManager.\n\nNote: Some systems may need manual configuration.", "OK");
        }

        private Type GetTypeByName(string className)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetTypes().FirstOrDefault(t => t.Name == className);
                if (type != null) return type;
            }
            return null;
        }

        #endregion

        #region Templates Tab (with Subtabs)

        private void DrawTemplatesTabWithSubtabs(float y, float height)
        {
            // Draw subtab bar
            float subTabY = y;
            float subTabHeight = 35;
            GUI.DrawTexture(new Rect(0, subTabY, position.width, subTabHeight), textures["bg_light"]);

            float subTabWidth = 90;
            float subTabStartX = 15;
            float subTabGap = 5;

            // Subtab buttons
            DrawSubTabButton(new Rect(subTabStartX, subTabY + 5, subTabWidth, 25), "Templates", TemplateSubTab.Templates);
            DrawSubTabButton(new Rect(subTabStartX + (subTabWidth + subTabGap), subTabY + 5, subTabWidth, 25), "Graph", TemplateSubTab.Graph);
            DrawSubTabButton(new Rect(subTabStartX + (subTabWidth + subTabGap) * 2, subTabY + 5, subTabWidth, 25), "Scenes", TemplateSubTab.Scenes);

            // Draw content based on selected subtab
            float contentY = y + subTabHeight;
            float contentHeight = height - subTabHeight;

            switch (templateSubTab)
            {
                case TemplateSubTab.Templates:
                    DrawTemplatesTab(contentY, contentHeight);
                    break;
                case TemplateSubTab.Graph:
                    DrawGraphTab(contentY, contentHeight);
                    break;
                case TemplateSubTab.Scenes:
                    DrawScenesTab(contentY, contentHeight);
                    break;
            }
        }

        private void DrawSubTabButton(Rect rect, string label, TemplateSubTab tab)
        {
            bool isActive = templateSubTab == tab;
            Color bgColor = isActive ? ACCENT_BLUE : BG_MEDIUM;
            Color textColor = isActive ? TEXT_WHITE : TEXT_GRAY;

            EditorGUI.DrawRect(rect, bgColor);

            var style = new GUIStyle() {
                fontSize = 11,
                fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor }
            };

            GUI.Label(rect, label, style);

            if (GUI.Button(rect, "", GUIStyle.none))
            {
                templateSubTab = tab;
                Repaint();
            }
        }

        private void DrawTemplatesTab(float y, float height)
        {
            mainScrollPos = GUI.BeginScrollView(new Rect(0, y, position.width, height), mainScrollPos,
                new Rect(0, 0, position.width - 20, gameTemplates.Count * 130 + 80));

            float padding = 20;
            float yPos = 20;

            GUI.Label(new Rect(padding, yPos, 400, 30), "Game Templates", new GUIStyle() {
                fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            GUI.Label(new Rect(padding, yPos + 28, 400, 20), "One-click system bundles for common game types", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            yPos += 65;

            foreach (var kvp in gameTemplates)
            {
                DrawTemplateCard(padding, yPos, position.width - 60, kvp.Key, kvp.Value);
                yPos += 120;
            }

            GUI.EndScrollView();
        }

        private void DrawTemplateCard(float x, float y, float width, string id, GameTemplate template)
        {
            var cardRect = new Rect(x, y, width, 105);
            GUI.DrawTexture(cardRect, textures["bg_card"]);

            EditorGUI.DrawRect(new Rect(x, y, 4, 105), template.color);

            GUI.Label(new Rect(x + 20, y + 15, width - 200, 24), template.name, new GUIStyle() {
                fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            GUI.Label(new Rect(x + 20, y + 42, width - 200, 20), template.description, new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });

            DrawBadge(new Rect(x + 20, y + 70, 80, 20), $"{template.systems.Length} systems", template.color);

            if (DrawButton(new Rect(x + width - 140, y + 35, 120, 35), "Use Template", template.color, true))
            {
                ApplyTemplate(template);
            }
        }

        private void ApplyTemplate(GameTemplate template)
        {
            foreach (var key in selectedSystems.Keys.ToList())
                selectedSystems[key] = false;

            foreach (var sysId in template.systems)
            {
                selectedSystems[sysId] = true;
                if (settings.autoSelectDependencies)
                    SelectDependencies(sysId);
            }

            currentTab = Tab.Library;
            DetectConflicts();

            EditorUtility.DisplayDialog("Template Applied",
                $"{template.name} applied!\n\n{template.systems.Length} systems selected.", "OK");
        }

        #endregion

        #region Favorites Tab

        private void DrawFavoritesTab(float y, float height)
        {
            if (favorites.Count == 0 && recentImports.Count == 0)
            {
                GUI.Label(new Rect(0, y + height / 2 - 30, position.width, 30), "No favorites or recent imports yet", new GUIStyle() {
                    fontSize = 14, normal = { textColor = TEXT_GRAY }, alignment = TextAnchor.MiddleCenter
                });
                return;
            }

            mainScrollPos = GUI.BeginScrollView(new Rect(0, y, position.width, height), mainScrollPos,
                new Rect(0, 0, position.width - 20, (favorites.Count + recentImports.Count) * 55 + 120));

            float padding = 20;
            float yPos = 20;

            if (favorites.Count > 0)
            {
                GUI.Label(new Rect(padding, yPos, 200, 25), "Favorites", new GUIStyle() {
                    fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = ACCENT_YELLOW }
                });
                yPos += 35;

                foreach (var sysId in favorites)
                {
                    var system = catalog?.systems?.FirstOrDefault(s => s.id == sysId);
                    if (system != null)
                    {
                        DrawCompactSystemItem(system, padding, yPos, position.width - 60);
                        yPos += 55;
                    }
                }
                yPos += 20;
            }

            if (recentImports.Count > 0)
            {
                GUI.Label(new Rect(padding, yPos, 200, 25), "Recent Imports", new GUIStyle() {
                    fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
                });
                yPos += 35;

                foreach (var sysId in recentImports.Take(10))
                {
                    var system = catalog?.systems?.FirstOrDefault(s => s.id == sysId);
                    if (system != null)
                    {
                        DrawCompactSystemItem(system, padding, yPos, position.width - 60);
                        yPos += 55;
                    }
                }
            }

            GUI.EndScrollView();
        }

        private void DrawCompactSystemItem(SystemData system, float x, float y, float width)
        {
            var itemRect = new Rect(x, y, width, 50);

            if (itemRect.Contains(Event.current.mousePosition))
            {
                GUI.DrawTexture(itemRect, textures["hover"]);
                Repaint();
            }

            bool isSelected = selectedSystems.ContainsKey(system.id) && selectedSystems[system.id];
            if (GUI.Button(new Rect(x + 10, y + 13, 24, 24), "", GUIStyle.none))
                selectedSystems[system.id] = !isSelected;

            EditorGUI.DrawRect(new Rect(x + 10, y + 13, 24, 24), isSelected ? ACCENT_GREEN : BG_LIGHT);
            if (isSelected)
                GUI.Label(new Rect(x + 10, y + 11, 24, 24), "+", new GUIStyle() {
                    fontSize = 16, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
                });

            GUI.Label(new Rect(x + 50, y + 8, width - 150, 22), system.name, new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            GUI.Label(new Rect(x + 50, y + 28, width - 150, 18), system.description, new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });

            if (DrawButton(new Rect(x + width - 90, y + 10, 80, 30), "Import", ACCENT_BLUE, true))
                ImportSingleSystem(system);
        }

        #endregion

        #region Settings Tab

        private void DrawSettingsTab(float y, float height)
        {
            float padding = 30;
            float yPos = y + 30;
            float labelWidth = 280;
            float rightColumn = position.width / 2 + 20;

            GUI.Label(new Rect(padding, yPos, 300, 30), "Settings", new GUIStyle() {
                fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            yPos += 50;

            // ═══════════════════════════════════════════════════════════════════
            // LEFT COLUMN - General & Import Settings
            // ═══════════════════════════════════════════════════════════════════

            float leftYPos = yPos;

            // General settings
            GUI.Label(new Rect(padding, leftYPos, 200, 22), "General", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            leftYPos += 30;

            settings.autoSelectDependencies = DrawToggleSetting(padding, leftYPos, labelWidth, "Auto-select dependencies",
                "Automatically select required dependencies", settings.autoSelectDependencies);
            leftYPos += 45;

            settings.showConflictWarnings = DrawToggleSetting(padding, leftYPos, labelWidth, "Show conflict warnings",
                "Display warnings for potential conflicts", settings.showConflictWarnings);
            leftYPos += 45;

            settings.confirmBeforeImport = DrawToggleSetting(padding, leftYPos, labelWidth, "Confirm before import",
                "Show confirmation dialog before importing", settings.confirmBeforeImport);
            leftYPos += 45;

            settings.autoCheckUpdates = DrawToggleSetting(padding, leftYPos, labelWidth, "Auto-check for updates",
                "Check for library updates on startup", settings.autoCheckUpdates);
            leftYPos += 55;

            EditorGUI.DrawRect(new Rect(padding, leftYPos, position.width / 2 - 50, 1), BORDER);
            leftYPos += 20;

            // Import Settings
            GUI.Label(new Rect(padding, leftYPos, 200, 22), "Import Settings", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            leftYPos += 30;

            GUI.Label(new Rect(padding, leftYPos, 120, 20), "Custom Namespace:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            settings.customNamespace = GUI.TextField(new Rect(padding + 120, leftYPos, 200, 22), settings.customNamespace ?? "");
            leftYPos += 25;
            GUI.Label(new Rect(padding, leftYPos, 350, 18), "Leave empty for default. Example: MyGame.Systems", new GUIStyle() {
                fontSize = 9, normal = { textColor = TEXT_GRAY }, fontStyle = FontStyle.Italic
            });
            leftYPos += 35;

            // Code Customizer
            GUI.Label(new Rect(padding, leftYPos, 200, 22), "Code Customizer", new GUIStyle() {
                fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            leftYPos += 25;

            settings.addHeaderComment = DrawMiniToggle(padding, leftYPos, "Add header comment", settings.addHeaderComment);
            leftYPos += 22;

            settings.addRegions = DrawMiniToggle(padding, leftYPos, "Add code regions", settings.addRegions);
            leftYPos += 22;

            settings.stripComments = DrawMiniToggle(padding, leftYPos, "Strip existing comments", settings.stripComments);
            leftYPos += 28;

            GUI.Label(new Rect(padding, leftYPos, 80, 20), "Author:", new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });
            settings.authorName = GUI.TextField(new Rect(padding + 80, leftYPos, 150, 20), settings.authorName ?? "");
            leftYPos += 35;

            EditorGUI.DrawRect(new Rect(padding, leftYPos, position.width / 2 - 50, 1), BORDER);
            leftYPos += 20;

            // Cloud sync
            GUI.Label(new Rect(padding, leftYPos, 200, 22), "Cloud Sync (GitHub Gist)", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            leftYPos += 30;

            GUI.Label(new Rect(padding, leftYPos, 100, 20), "Gist Token:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            settings.githubToken = GUI.PasswordField(new Rect(padding + 100, leftYPos, 220, 22), settings.githubToken ?? "", '*');
            leftYPos += 28;

            GUI.Label(new Rect(padding, leftYPos, 100, 20), "Gist ID:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            settings.gistId = GUI.TextField(new Rect(padding + 100, leftYPos, 220, 22), settings.gistId ?? "");
            leftYPos += 32;

            if (DrawButton(new Rect(padding, leftYPos, 110, 26), "Backup", ACCENT_BLUE, !string.IsNullOrEmpty(settings.githubToken)))
                BackupToGist();

            if (DrawButton(new Rect(padding + 115, leftYPos, 110, 26), "Restore", ACCENT_GREEN,
                !string.IsNullOrEmpty(settings.githubToken) && !string.IsNullOrEmpty(settings.gistId)))
                RestoreFromGist();

            // ═══════════════════════════════════════════════════════════════════
            // RIGHT COLUMN - Updates, AI & Library Info
            // ═══════════════════════════════════════════════════════════════════

            float rightYPos = yPos;

            // Update Check Section
            GUI.Label(new Rect(rightColumn, rightYPos, 200, 22), "Updates", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            rightYPos += 30;

            // Version info
            GUI.Label(new Rect(rightColumn, rightYPos, 100, 20), "Current:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            GUI.Label(new Rect(rightColumn + 100, rightYPos, 100, 20), $"v{VERSION}", new GUIStyle() {
                fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });
            rightYPos += 22;

            if (!string.IsNullOrEmpty(latestVersion))
            {
                GUI.Label(new Rect(rightColumn, rightYPos, 100, 20), "Latest:", new GUIStyle() {
                    fontSize = 11, normal = { textColor = TEXT_GRAY }
                });
                GUI.Label(new Rect(rightColumn + 100, rightYPos, 100, 20), $"v{latestVersion}", new GUIStyle() {
                    fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = updateAvailable ? ACCENT_GREEN : TEXT_WHITE }
                });
                rightYPos += 22;
            }

            if (updateAvailable)
            {
                EditorGUI.DrawRect(new Rect(rightColumn, rightYPos, 280, 28), new Color(ACCENT_GREEN.r, ACCENT_GREEN.g, ACCENT_GREEN.b, 0.15f));
                GUI.Label(new Rect(rightColumn + 10, rightYPos + 4, 260, 20), "+ New version available!", new GUIStyle() {
                    fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = ACCENT_GREEN }
                });
                rightYPos += 32;
            }
            else if (!string.IsNullOrEmpty(latestVersion))
            {
                GUI.Label(new Rect(rightColumn, rightYPos, 200, 18), "You're up to date", new GUIStyle() {
                    fontSize = 10, normal = { textColor = TEXT_GRAY }
                });
                rightYPos += 22;
            }

            rightYPos += 5;

            string checkBtnText = isCheckingUpdate ? "Checking..." : "Check for Updates";
            if (DrawButton(new Rect(rightColumn, rightYPos, 150, 28), checkBtnText, ACCENT_BLUE, !isCheckingUpdate))
                CheckForUpdates();

            if (!string.IsNullOrEmpty(lastUpdateCheck))
            {
                GUI.Label(new Rect(rightColumn + 160, rightYPos + 6, 150, 18), $"Last: {lastUpdateCheck}", new GUIStyle() {
                    fontSize = 9, normal = { textColor = TEXT_GRAY }
                });
            }
            rightYPos += 50;

            EditorGUI.DrawRect(new Rect(rightColumn, rightYPos, position.width / 2 - 50, 1), BORDER);
            rightYPos += 20;

            // AI Integration Section
            GUI.Label(new Rect(rightColumn, rightYPos, 200, 22), "AI Integration (Claude)", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            rightYPos += 30;

            GUI.Label(new Rect(rightColumn, rightYPos, 100, 20), "API Key:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            settings.claudeApiKey = GUI.PasswordField(new Rect(rightColumn + 100, rightYPos, 220, 22), settings.claudeApiKey ?? "", '*');
            rightYPos += 25;
            GUI.Label(new Rect(rightColumn, rightYPos, 350, 18), "Get your key from console.anthropic.com", new GUIStyle() {
                fontSize = 9, normal = { textColor = TEXT_GRAY }, fontStyle = FontStyle.Italic
            });
            rightYPos += 28;

            bool hasApiKey = !string.IsNullOrEmpty(settings.claudeApiKey);
            string aiStatus = hasApiKey ? "* Connected" : "- Not configured";
            Color aiStatusColor = hasApiKey ? ACCENT_GREEN : TEXT_GRAY;
            GUI.Label(new Rect(rightColumn, rightYPos, 200, 20), aiStatus, new GUIStyle() {
                fontSize = 11, normal = { textColor = aiStatusColor }
            });
            rightYPos += 35;

            EditorGUI.DrawRect(new Rect(rightColumn, rightYPos, position.width / 2 - 50, 1), BORDER);
            rightYPos += 20;

            // Library info
            GUI.Label(new Rect(rightColumn, rightYPos, 200, 22), "Library Information", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            rightYPos += 30;

            GUI.Label(new Rect(rightColumn, rightYPos, 80, 20), "Location:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }
            });
            GUI.Label(new Rect(rightColumn + 80, rightYPos, 280, 20), libraryPath, new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_LIGHT }
            });
            rightYPos += 22;

            if (catalogLoaded)
            {
                GUI.Label(new Rect(rightColumn, rightYPos, 80, 20), "Systems:", new GUIStyle() {
                    fontSize = 11, normal = { textColor = TEXT_GRAY }
                });
                GUI.Label(new Rect(rightColumn + 80, rightYPos, 200, 20), $"{catalog.systems.Length} available", new GUIStyle() {
                    fontSize = 11, normal = { textColor = TEXT_LIGHT }
                });
                rightYPos += 22;

                int localCount = catalog.systems.Count(s => IsSystemInLibrary(s.id));
                GUI.Label(new Rect(rightColumn, rightYPos, 80, 20), "Local:", new GUIStyle() {
                    fontSize = 11, normal = { textColor = TEXT_GRAY }
                });
                GUI.Label(new Rect(rightColumn + 80, rightYPos, 200, 20), $"{localCount} cached", new GUIStyle() {
                    fontSize = 11, normal = { textColor = TEXT_LIGHT }
                });
            }
            rightYPos += 35;

            EditorGUI.DrawRect(new Rect(rightColumn, rightYPos, position.width / 2 - 50, 1), BORDER);
            rightYPos += 20;

            // Project Analyzer Section
            GUI.Label(new Rect(rightColumn, rightYPos, 200, 22), "Project Analyzer", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_LIGHT }
            });
            rightYPos += 28;

            string analyzeBtnText = isAnalyzing ? "Analyzing..." : "Analyze Project";
            if (DrawButton(new Rect(rightColumn, rightYPos, 140, 28), analyzeBtnText, ACCENT_PURPLE, !isAnalyzing))
            {
                AnalyzeProject();
                ShowAnalysisResults();
            }

            if (lastAnalysis != null)
            {
                if (DrawButton(new Rect(rightColumn + 150, rightYPos, 120, 28), "View Results", ACCENT_BLUE, true))
                    ShowAnalysisResults();
            }
            rightYPos += 32;

            // Show quick stats if analysis exists
            if (lastAnalysis != null)
            {
                GUI.Label(new Rect(rightColumn, rightYPos, 280, 16),
                    $"{lastAnalysis.projectType} • {lastAnalysis.installedSystems.Count} systems installed", new GUIStyle() {
                    fontSize = 9, normal = { textColor = TEXT_GRAY }
                });
                if (lastAnalysis.suggestedSystems.Count > 0)
                {
                    rightYPos += 14;
                    GUI.Label(new Rect(rightColumn, rightYPos, 280, 16),
                        $"{lastAnalysis.suggestedSystems.Count} suggestions available", new GUIStyle() {
                        fontSize = 9, normal = { textColor = ACCENT_CYAN }
                    });
                }
            }
            rightYPos += 30;

            // Action buttons - bottom of right column
            if (DrawButton(new Rect(rightColumn, rightYPos, 90, 30), "Library", ACCENT_BLUE, true))
                EditorUtility.RevealInFinder(libraryPath);

            if (DrawButton(new Rect(rightColumn + 95, rightYPos, 90, 30), "Clear Cache", ACCENT_ORANGE, Directory.Exists(cachePath)))
            {
                if (EditorUtility.DisplayDialog("Clear Cache",
                    "Delete all cached files?\nThis will force re-download from GitHub.", "Clear", "Cancel"))
                {
                    ClearCache();
                }
            }

            if (DrawButton(new Rect(rightColumn + 190, rightYPos, 80, 30), "Refresh", ACCENT_GREEN, true))
            {
                LoadCatalog();
                DetectConflicts();
            }
        }

        private void ClearCache()
        {
            try
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                    Directory.CreateDirectory(cachePath);
                    Debug.Log("[UnityVault] Cache cleared successfully");
                    EditorUtility.DisplayDialog("Cache Cleared", "All cached files have been deleted.", "OK");
                    Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityVault] Failed to clear cache: {e.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to clear cache:\n{e.Message}", "OK");
            }
        }

        private bool DrawToggleSetting(float x, float y, float labelWidth, string label, string description, bool value)
        {
            GUI.Label(new Rect(x, y, labelWidth, 22), label, new GUIStyle() {
                fontSize = 12, normal = { textColor = TEXT_WHITE }
            });
            bool result = GUI.Toggle(new Rect(x + labelWidth, y, 24, 22), value, "");
            GUI.Label(new Rect(x, y + 22, 400, 18), description, new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });
            return result;
        }

        private bool DrawMiniToggle(float x, float y, string label, bool value)
        {
            bool result = GUI.Toggle(new Rect(x, y, 18, 18), value, "");
            GUI.Label(new Rect(x + 22, y, 200, 18), label, new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_LIGHT }
            });
            return result;
        }

        #endregion

        #region AI Tab

        private void DrawAITab(float y, float height)
        {
            float padding = 20;
            float chatWidth = position.width - padding * 2;

            // Check if API key is set
            if (string.IsNullOrEmpty(settings.claudeApiKey))
            {
                DrawAISetupScreen(y, height);
                return;
            }

            // Header
            GUI.Label(new Rect(padding, y + 15, 300, 30), "AI Assistant", new GUIStyle() {
                fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            // Status indicator
            string statusText = isAIResponding ? "* Thinking..." : "* Ready";
            Color statusColor = isAIResponding ? ACCENT_ORANGE : ACCENT_GREEN;
            GUI.Label(new Rect(padding + 160, y + 20, 150, 20), statusText, new GUIStyle() {
                fontSize = 11, normal = { textColor = statusColor }
            });

            // Clear chat button
            if (DrawButton(new Rect(position.width - padding - 100, y + 15, 90, 26), "Clear Chat", ACCENT_RED, chatHistory.Count > 0))
            {
                chatHistory.Clear();
            }

            float chatY = y + 55;
            float chatHeight = height - 150;
            float inputY = y + height - 85;

            // Chat area background
            EditorGUI.DrawRect(new Rect(padding, chatY, chatWidth, chatHeight), BG_CODE);
            EditorGUI.DrawRect(new Rect(padding, chatY, chatWidth, 2), ACCENT_PURPLE);

            // Chat messages
            DrawChatMessages(padding, chatY, chatWidth, chatHeight);

            // Input area
            DrawChatInput(padding, inputY, chatWidth);

            // Quick actions
            DrawQuickActions(padding, inputY + 45, chatWidth);
        }

        private void DrawAISetupScreen(float y, float height)
        {
            float centerX = position.width / 2;
            float boxWidth = 400;
            float boxX = centerX - boxWidth / 2;

            // Background box
            EditorGUI.DrawRect(new Rect(boxX, y + 80, boxWidth, 280), BG_CARD);
            EditorGUI.DrawRect(new Rect(boxX, y + 80, boxWidth, 3), ACCENT_PURPLE);

            // Icon and title
            GUI.Label(new Rect(boxX, y + 100, boxWidth, 40), "AI", new GUIStyle() {
                fontSize = 28, fontStyle = FontStyle.Bold, normal = { textColor = ACCENT_PURPLE }, alignment = TextAnchor.MiddleCenter
            });
            GUI.Label(new Rect(boxX, y + 145, boxWidth, 30), "AI Assistant Setup", new GUIStyle() {
                fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }, alignment = TextAnchor.MiddleCenter
            });

            GUI.Label(new Rect(boxX + 20, y + 180, boxWidth - 40, 40), "Enter your Claude API key to enable AI-powered code generation and assistance.", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }, alignment = TextAnchor.MiddleCenter, wordWrap = true
            });

            // API Key input
            GUI.Label(new Rect(boxX + 30, y + 230, 80, 20), "API Key:", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_LIGHT }
            });
            settings.claudeApiKey = GUI.PasswordField(new Rect(boxX + 30, y + 250, boxWidth - 60, 24), settings.claudeApiKey ?? "", '*');

            // Help text
            GUI.Label(new Rect(boxX + 30, y + 280, boxWidth - 60, 20), "Get your key from console.anthropic.com", new GUIStyle() {
                fontSize = 9, normal = { textColor = TEXT_GRAY }, fontStyle = FontStyle.Italic
            });

            // Connect button
            if (DrawButton(new Rect(centerX - 60, y + 310, 120, 32), "Connect", ACCENT_PURPLE, !string.IsNullOrEmpty(settings.claudeApiKey)))
            {
                // Add welcome message
                chatHistory.Add(new ChatMessage(false, "Hello! I'm your AI assistant. I can help you with:\n\n• Generating Unity scripts\n• Explaining code\n• Fixing bugs\n• Answering questions about game development\n\nHow can I help you today?"));
                Repaint();
            }
        }

        private void DrawChatMessages(float x, float y, float width, float height)
        {
            if (chatHistory.Count == 0)
            {
                GUI.Label(new Rect(x, y + height / 2 - 30, width, 60),
                    "Start a conversation...\nAsk me to generate code, explain concepts, or help with bugs.",
                    new GUIStyle() {
                        fontSize = 12, normal = { textColor = TEXT_GRAY }, alignment = TextAnchor.MiddleCenter, wordWrap = true
                    });
                return;
            }

            float contentHeight = CalculateChatHeight();
            chatScrollPos = GUI.BeginScrollView(
                new Rect(x, y + 5, width, height - 10),
                chatScrollPos,
                new Rect(0, 0, width - 20, contentHeight)
            );

            float msgY = 10;
            foreach (var msg in chatHistory)
            {
                msgY += DrawChatMessage(msg, 10, msgY, width - 40);
            }

            GUI.EndScrollView();

            // Auto-scroll to bottom when new message
            if (isAIResponding || (chatHistory.Count > 0 && chatHistory[chatHistory.Count - 1].timestamp > DateTime.Now.AddSeconds(-1)))
            {
                chatScrollPos.y = contentHeight;
            }
        }

        private float DrawChatMessage(ChatMessage msg, float x, float y, float width)
        {
            float bubbleWidth = width * 0.85f;
            float bubbleX = msg.isUser ? x + width - bubbleWidth : x;

            Color bubbleColor = msg.isUser ? new Color(0.2f, 0.35f, 0.5f) : new Color(0.18f, 0.18f, 0.18f);
            Color textColor = TEXT_WHITE;

            // Calculate message height
            var style = new GUIStyle() { fontSize = 11, normal = { textColor = textColor }, wordWrap = true };
            float textHeight = style.CalcHeight(new GUIContent(msg.content), bubbleWidth - 20);

            float codeHeight = 0;
            if (!string.IsNullOrEmpty(msg.codeBlock))
            {
                codeHeight = 100 + 30; // Fixed height for code block + buttons
            }

            float totalHeight = textHeight + codeHeight + 25;

            // Draw bubble
            EditorGUI.DrawRect(new Rect(bubbleX, y, bubbleWidth, totalHeight), bubbleColor);

            // Sender label
            string sender = msg.isUser ? "You" : "Claude";
            GUI.Label(new Rect(bubbleX + 10, y + 5, 100, 16), sender, new GUIStyle() {
                fontSize = 9, fontStyle = FontStyle.Bold, normal = { textColor = msg.isUser ? ACCENT_BLUE : ACCENT_PURPLE }
            });

            // Message text
            GUI.Label(new Rect(bubbleX + 10, y + 22, bubbleWidth - 20, textHeight), msg.content, style);

            // Code block
            if (!string.IsNullOrEmpty(msg.codeBlock))
            {
                float codeY = y + 22 + textHeight + 5;
                EditorGUI.DrawRect(new Rect(bubbleX + 10, codeY, bubbleWidth - 20, 100), BG_CODE);

                GUI.Label(new Rect(bubbleX + 15, codeY + 5, bubbleWidth - 30, 90), msg.codeBlock, new GUIStyle() {
                    fontSize = 10, normal = { textColor = ACCENT_CYAN }, wordWrap = true
                });

                // Code action buttons
                if (DrawButton(new Rect(bubbleX + 10, codeY + 105, 70, 22), "Copy", ACCENT_BLUE, true))
                {
                    EditorGUIUtility.systemCopyBuffer = msg.codeBlock;
                    Debug.Log("[UnityVault] Code copied to clipboard");
                }

                if (DrawButton(new Rect(bubbleX + 85, codeY + 105, 90, 22), "Save File", ACCENT_GREEN, true))
                {
                    SaveCodeToFile(msg.codeBlock);
                }
            }

            return totalHeight + 10;
        }

        private float CalculateChatHeight()
        {
            float height = 20;
            float width = position.width - 80;
            var style = new GUIStyle() { fontSize = 11, wordWrap = true };

            foreach (var msg in chatHistory)
            {
                float bubbleWidth = width * 0.85f;
                float textHeight = style.CalcHeight(new GUIContent(msg.content), bubbleWidth - 20);
                float codeHeight = string.IsNullOrEmpty(msg.codeBlock) ? 0 : 130;
                height += textHeight + codeHeight + 35;
            }

            return height;
        }

        private void DrawChatInput(float x, float y, float width)
        {
            EditorGUI.DrawRect(new Rect(x, y, width, 38), BG_MEDIUM);

            // Input field
            chatInput = GUI.TextField(new Rect(x + 10, y + 8, width - 110, 22), chatInput, new GUIStyle(GUI.skin.textField) {
                fontSize = 12, padding = new RectOffset(8, 8, 4, 4)
            });

            // Send button
            bool canSend = !string.IsNullOrEmpty(chatInput) && !isAIResponding;
            if (DrawButton(new Rect(x + width - 90, y + 5, 80, 28), isAIResponding ? "..." : "Send >", ACCENT_PURPLE, canSend))
            {
                SendMessage();
            }

            // Handle Enter key
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && canSend)
            {
                SendMessage();
                Event.current.Use();
            }
        }

        private void DrawQuickActions(float x, float y, float width)
        {
            GUI.Label(new Rect(x, y, 100, 20), "Quick Actions:", new GUIStyle() {
                fontSize = 10, normal = { textColor = TEXT_GRAY }
            });

            float btnX = x + 90;
            float btnWidth = 110;

            if (DrawButton(new Rect(btnX, y - 2, btnWidth, 24), "Generate System", ACCENT_GREEN, !isAIResponding))
            {
                chatInput = "Generate a Unity script for ";
                GUI.FocusControl("");
            }

            if (DrawButton(new Rect(btnX + btnWidth + 5, y - 2, btnWidth, 24), "Explain Code", ACCENT_BLUE, !isAIResponding))
            {
                chatInput = "Explain this code: ";
                GUI.FocusControl("");
            }

            if (DrawButton(new Rect(btnX + (btnWidth + 5) * 2, y - 2, btnWidth, 24), "Fix Bug", ACCENT_ORANGE, !isAIResponding))
            {
                chatInput = "Help me fix this bug: ";
                GUI.FocusControl("");
            }

            if (DrawButton(new Rect(btnX + (btnWidth + 5) * 3, y - 2, btnWidth, 24), "Optimize", ACCENT_CYAN, !isAIResponding))
            {
                chatInput = "Optimize this code for performance: ";
                GUI.FocusControl("");
            }
        }

        private void SendMessage()
        {
            if (string.IsNullOrEmpty(chatInput) || isAIResponding) return;

            string userMessage = chatInput;
            chatHistory.Add(new ChatMessage(true, userMessage));
            chatInput = "";
            isAIResponding = true;
            Repaint();

            // Send to Claude API
            EditorApplication.delayCall += () => SendToClaudeAPI(userMessage);
        }

        private void SendToClaudeAPI(string userMessage)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("x-api-key", settings.claudeApiKey);
                    client.Headers.Add("anthropic-version", "2023-06-01");
                    client.Headers.Add("Content-Type", "application/json");

                    // Build conversation context
                    var messagesJson = new StringBuilder();
                    messagesJson.Append("[");

                    // System message
                    messagesJson.Append("{\"role\":\"user\",\"content\":\"You are a Unity game development expert assistant. Help with C# scripting, game systems, and best practices. When providing code, make it production-ready with proper error handling and comments. Keep responses concise but complete.\"},");
                    messagesJson.Append("{\"role\":\"assistant\",\"content\":\"I understand. I'll help you with Unity development, providing clean, production-ready C# code with best practices.\"},");

                    // Add recent chat history (last 10 messages for context)
                    var recentMessages = chatHistory.Skip(Math.Max(0, chatHistory.Count - 10)).ToList();
                    for (int i = 0; i < recentMessages.Count - 1; i++) // Exclude the message we just added
                    {
                        var msg = recentMessages[i];
                        string role = msg.isUser ? "user" : "assistant";
                        string content = EscapeJson(msg.content);
                        messagesJson.Append($"{{\"role\":\"{role}\",\"content\":\"{content}\"}},");
                    }

                    // Current message
                    messagesJson.Append($"{{\"role\":\"user\",\"content\":\"{EscapeJson(userMessage)}\"}}");
                    messagesJson.Append("]");

                    string jsonRequest = $@"{{
                        ""model"": ""claude-sonnet-4-20250514"",
                        ""max_tokens"": 4000,
                        ""messages"": {messagesJson}
                    }}";

                    string response = client.UploadString("https://api.anthropic.com/v1/messages", jsonRequest);

                    // Parse response
                    string assistantMessage = ParseClaudeResponse(response);

                    // Extract code block if present
                    string codeBlock = null;
                    var codeMatch = Regex.Match(assistantMessage, @"```(?:csharp|cs)?\s*([\s\S]*?)```");
                    if (codeMatch.Success)
                    {
                        codeBlock = codeMatch.Groups[1].Value.Trim();
                        // Clean up the message text
                        assistantMessage = Regex.Replace(assistantMessage, @"```(?:csharp|cs)?\s*[\s\S]*?```", "[Code block below]").Trim();
                    }

                    chatHistory.Add(new ChatMessage(false, assistantMessage, codeBlock));
                }
            }
            catch (Exception e)
            {
                chatHistory.Add(new ChatMessage(false, $"Error: {e.Message}\n\nPlease check your API key and try again."));
                Debug.LogError($"[UnityVault] AI API error: {e.Message}");
            }
            finally
            {
                isAIResponding = false;
                Repaint();
            }
        }

        private string EscapeJson(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private void SaveCodeToFile(string code)
        {
            string path = EditorUtility.SaveFilePanel("Save Script", "Assets", "NewScript.cs", "cs");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, code);
                AssetDatabase.Refresh();
                Debug.Log($"[UnityVault] Script saved to: {path}");
                EditorUtility.DisplayDialog("Saved", $"Script saved to:\n{path}", "OK");
            }
        }

        #endregion

        #region Footer

        private void DrawFooter()
        {
            float footerY = position.height - 90;
            GUI.DrawTexture(new Rect(0, footerY, position.width, 90), textures["header"]);
            EditorGUI.DrawRect(new Rect(0, footerY, position.width, 1), BORDER);

            var selected = selectedSystems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            int readyCount = selected.Count(id => IsSystemInLibrary(id) || IsSystemInProject(id));
            int generateCount = selected.Count - readyCount;
            int conflictCount = detectedConflicts.Count(c => selected.Contains(c.systemId));

            GUI.Label(new Rect(20, footerY + 10, 200, 22), $"Selected: {selected.Count} systems", new GUIStyle() {
                fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }
            });

            // Clear All button
            if (selected.Count > 0)
            {
                if (DrawButton(new Rect(180, footerY + 8, 80, 24), "Clear All", ACCENT_RED, true))
                {
                    ClearAllSelections();
                }
            }

            string detailText = $"Ready: {readyCount}  •  Generate: {generateCount}";
            if (conflictCount > 0)
                detailText += $"  •  Conflicts: {conflictCount}";

            GUI.Label(new Rect(20, footerY + 32, 400, 18), detailText, new GUIStyle() {
                fontSize = 10, normal = { textColor = conflictCount > 0 ? ACCENT_ORANGE : TEXT_GRAY }
            });

            float btnWidth = 180;
            float btnY = footerY + 45;
            float btnX = position.width - btnWidth * 2 - 40;

            if (DrawButton(new Rect(btnX, btnY, btnWidth, 36), "Generate & Import", ACCENT_GREEN, selected.Count > 0))
            {
                if (conflictCount > 0 && !EditorUtility.DisplayDialog("Conflicts Detected",
                    $"{conflictCount} systems have potential conflicts.\n\nContinue anyway?", "Import", "Cancel"))
                    return;

                if (!settings.confirmBeforeImport || EditorUtility.DisplayDialog("Import Systems",
                    $"Import {selected.Count} systems?", "Import", "Cancel"))
                {
                    ImportSelectedSystems();
                }
            }

            if (DrawButton(new Rect(btnX + btnWidth + 10, btnY, btnWidth, 36), "Export Package", ACCENT_BLUE, selected.Count > 0))
            {
                ExportSelectedSystems();
            }
        }

        #endregion

        #region Conflict Detection

        private void DetectConflicts()
        {
            detectedConflicts.Clear();

            if (!catalogLoaded) return;

            // Scan project for existing classes
            var projectScripts = AssetDatabase.FindAssets("t:Script", new[] { "Assets" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Where(path => !path.Contains("UnityVault/Generated"))
                .ToList();

            var existingClasses = new HashSet<string>();
            var existingNamespaces = new HashSet<string>();

            foreach (var scriptPath in projectScripts)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script != null)
                {
                    var type = script.GetClass();
                    if (type != null)
                    {
                        existingClasses.Add(type.Name);
                        if (!string.IsNullOrEmpty(type.Namespace))
                            existingNamespaces.Add(type.Namespace);
                    }
                }
            }

            // Check each system for conflicts
            foreach (var system in catalog.systems)
            {
                string mainClass = system.name.Replace(" ", "");

                if (existingClasses.Contains(mainClass))
                {
                    detectedConflicts.Add(new ConflictInfo {
                        systemId = system.id,
                        systemName = system.name,
                        conflictType = "Class name conflict",
                        details = $"Class '{mainClass}' already exists in project"
                    });
                }
            }
        }

        private void ShowConflictDetails()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Detected Conflicts:\n");

            foreach (var conflict in detectedConflicts)
            {
                sb.AppendLine($"• {conflict.systemName}");
                sb.AppendLine($"  Type: {conflict.conflictType}");
                sb.AppendLine($"  {conflict.details}\n");
            }

            EditorUtility.DisplayDialog("Conflict Details", sb.ToString(), "OK");
        }

        #endregion

        #region Project Analyzer

        private void AnalyzeProject()
        {
            if (isAnalyzing) return;
            isAnalyzing = true;

            lastAnalysis = new ProjectAnalysis();
            lastAnalysis.analysisTime = DateTime.Now;

            // Count scripts
            var scripts = AssetDatabase.FindAssets("t:Script", new[] { "Assets" });
            lastAnalysis.totalScripts = scripts.Length;

            // Count scenes
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            lastAnalysis.totalScenes = scenes.Length;

            // Detect installed UnityVault systems
            string vaultPath = "Assets/UnityVault/Generated";
            if (Directory.Exists(vaultPath))
            {
                var installedDirs = Directory.GetDirectories(vaultPath);
                foreach (var dir in installedDirs)
                {
                    string systemName = Path.GetFileName(dir);
                    var matchingSystem = catalog?.systems?.FirstOrDefault(s =>
                        Path.GetFileName(s.path).Equals(systemName, StringComparison.OrdinalIgnoreCase));
                    if (matchingSystem != null)
                    {
                        lastAnalysis.installedSystems.Add(matchingSystem.id);
                    }
                }
            }

            // Analyze code patterns to detect project type and suggest systems
            DetectProjectPatterns();

            // Generate suggestions based on installed systems
            GenerateSuggestions();

            isAnalyzing = false;
            showAnalysisPopup = true;
            Repaint();
        }

        private void DetectProjectPatterns()
        {
            var allScripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
            var patterns = new Dictionary<string, int>();

            foreach (var guid in allScripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Editor") || path.Contains("Test")) continue;

                string content = "";
                try { content = File.ReadAllText(path); } catch { continue; }

                // Detect patterns
                if (content.Contains("CharacterController") || content.Contains("Rigidbody"))
                    patterns["Movement"] = patterns.GetValueOrDefault("Movement") + 1;
                if (content.Contains("Health") || content.Contains("TakeDamage") || content.Contains("IDamageable"))
                    patterns["Health"] = patterns.GetValueOrDefault("Health") + 1;
                if (content.Contains("Inventory") || content.Contains("Item") || content.Contains("Slot"))
                    patterns["Inventory"] = patterns.GetValueOrDefault("Inventory") + 1;
                if (content.Contains("NavMesh") || content.Contains("AIController") || content.Contains("Patrol"))
                    patterns["AI"] = patterns.GetValueOrDefault("AI") + 1;
                if (content.Contains("Canvas") || content.Contains("UI") || content.Contains("Button"))
                    patterns["UI"] = patterns.GetValueOrDefault("UI") + 1;
                if (content.Contains("AudioSource") || content.Contains("PlaySound") || content.Contains("SFX"))
                    patterns["Audio"] = patterns.GetValueOrDefault("Audio") + 1;
                if (content.Contains("SaveData") || content.Contains("PlayerPrefs") || content.Contains("JsonUtility"))
                    patterns["SaveLoad"] = patterns.GetValueOrDefault("SaveLoad") + 1;
                if (content.Contains("Quest") || content.Contains("Dialogue") || content.Contains("NPC"))
                    patterns["RPG"] = patterns.GetValueOrDefault("RPG") + 1;
            }

            foreach (var kvp in patterns)
                lastAnalysis.detectedPatterns.Add($"{kvp.Key}: {kvp.Value} scripts");

            // Determine project type
            if (patterns.GetValueOrDefault("RPG") > 2 && patterns.GetValueOrDefault("Inventory") > 0)
                lastAnalysis.projectType = "RPG";
            else if (patterns.GetValueOrDefault("Movement") > 3 && patterns.GetValueOrDefault("Health") > 0)
                lastAnalysis.projectType = "Action Game";
            else if (patterns.GetValueOrDefault("UI") > 5)
                lastAnalysis.projectType = "UI-Heavy Application";
            else if (patterns.GetValueOrDefault("AI") > 2)
                lastAnalysis.projectType = "AI-Focused Game";
            else if (lastAnalysis.totalScripts > 50)
                lastAnalysis.projectType = "Large Project";
            else if (lastAnalysis.totalScripts > 10)
                lastAnalysis.projectType = "Medium Project";
            else
                lastAnalysis.projectType = "Small Project";

            lastAnalysis.categoryUsage = patterns;
        }

        private void GenerateSuggestions()
        {
            if (catalog?.systems == null) return;

            var installed = new HashSet<string>(lastAnalysis.installedSystems);
            var patterns = lastAnalysis.categoryUsage;

            // Suggest based on patterns
            if (patterns.GetValueOrDefault("Health") > 0 && !installed.Contains("health_system"))
                lastAnalysis.suggestedSystems.Add("health_system");
            if (patterns.GetValueOrDefault("Movement") > 0 && !installed.Contains("character_controller_3d"))
                lastAnalysis.suggestedSystems.Add("character_controller_3d");
            if (patterns.GetValueOrDefault("Inventory") > 0 && !installed.Contains("inventory_system"))
                lastAnalysis.suggestedSystems.Add("inventory_system");
            if (patterns.GetValueOrDefault("AI") > 0 && !installed.Contains("ai_state_machine"))
                lastAnalysis.suggestedSystems.Add("ai_state_machine");
            if (patterns.GetValueOrDefault("Audio") > 0 && !installed.Contains("audio_manager"))
                lastAnalysis.suggestedSystems.Add("audio_manager");
            if (patterns.GetValueOrDefault("SaveLoad") > 0 && !installed.Contains("save_load"))
                lastAnalysis.suggestedSystems.Add("save_load");

            // Suggest dependencies for installed systems
            foreach (var sysId in installed)
            {
                var system = catalog.systems.FirstOrDefault(s => s.id == sysId);
                if (system?.dependencies != null)
                {
                    foreach (var dep in system.dependencies)
                    {
                        if (!installed.Contains(dep) && !lastAnalysis.suggestedSystems.Contains(dep))
                            lastAnalysis.suggestedSystems.Add(dep);
                    }
                }
            }

            // Limit suggestions to top 5
            if (lastAnalysis.suggestedSystems.Count > 5)
                lastAnalysis.suggestedSystems = lastAnalysis.suggestedSystems.Take(5).ToList();
        }

        private void ShowAnalysisResults()
        {
            if (lastAnalysis == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"PROJECT ANALYSIS");
            sb.AppendLine($"═══════════════════════════════════\n");
            sb.AppendLine($"Project Type: {lastAnalysis.projectType}");
            sb.AppendLine($"Total Scripts: {lastAnalysis.totalScripts}");
            sb.AppendLine($"Total Scenes: {lastAnalysis.totalScenes}");
            sb.AppendLine($"Analysis Time: {lastAnalysis.analysisTime:HH:mm:ss}\n");

            sb.AppendLine($"INSTALLED SYSTEMS ({lastAnalysis.installedSystems.Count})");
            sb.AppendLine($"───────────────────────────────────");
            if (lastAnalysis.installedSystems.Count > 0)
            {
                foreach (var sysId in lastAnalysis.installedSystems)
                {
                    var sys = catalog?.systems?.FirstOrDefault(s => s.id == sysId);
                    sb.AppendLine($"  + {sys?.name ?? sysId}");
                }
            }
            else
            {
                sb.AppendLine("  No UnityVault systems installed");
            }
            sb.AppendLine();

            sb.AppendLine($"DETECTED PATTERNS");
            sb.AppendLine($"───────────────────────────────────");
            foreach (var pattern in lastAnalysis.detectedPatterns)
                sb.AppendLine($"  • {pattern}");
            sb.AppendLine();

            if (lastAnalysis.suggestedSystems.Count > 0)
            {
                sb.AppendLine($"SUGGESTED SYSTEMS");
                sb.AppendLine($"───────────────────────────────────");
                foreach (var sysId in lastAnalysis.suggestedSystems)
                {
                    var sys = catalog?.systems?.FirstOrDefault(s => s.id == sysId);
                    sb.AppendLine($"  > {sys?.name ?? sysId}");
                }
            }

            EditorUtility.DisplayDialog("Project Analysis", sb.ToString(), "OK");
        }

        #endregion

        #region Cloud Sync

        private void BackupToGist()
        {
            try
            {
                var backupData = new {
                    favorites = favorites.ToArray(),
                    recentImports = recentImports.ToArray(),
                    settings = settings
                };

                string json = JsonUtility.ToJson(backupData, true);

                // Create or update gist
                using (var client = new WebClient())
                {
                    client.Headers.Add("Authorization", $"token {settings.githubToken}");
                    client.Headers.Add("User-Agent", "UnityVault");
                    client.Headers.Add("Content-Type", "application/json");

                    var gistData = new {
                        description = "UnityVault Backup",
                        @public = false,
                        files = new Dictionary<string, object> {
                            { "unityvault_backup.json", new { content = json } }
                        }
                    };

                    string gistJson = JsonUtility.ToJson(gistData);

                    string url = string.IsNullOrEmpty(settings.gistId) ? GITHUB_GIST_API : $"{GITHUB_GIST_API}/{settings.gistId}";
                    string method = string.IsNullOrEmpty(settings.gistId) ? "POST" : "PATCH";

                    // Simple POST (create new gist)
                    string response = client.UploadString(url, gistJson);

                    EditorUtility.DisplayDialog("Backup Complete", "Settings backed up to GitHub Gist!", "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Backup Failed", $"Error: {e.Message}", "OK");
            }
        }

        private void RestoreFromGist()
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("Authorization", $"token {settings.githubToken}");
                    client.Headers.Add("User-Agent", "UnityVault");

                    string response = client.DownloadString($"{GITHUB_GIST_API}/{settings.gistId}");

                    // Parse gist response and extract file content
                    // This is simplified - you'd want proper JSON parsing
                    EditorUtility.DisplayDialog("Restore", "Restore from Gist - Implementation needed for full JSON parsing", "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Restore Failed", $"Error: {e.Message}", "OK");
            }
        }

        private void CheckForUpdates()
        {
            if (isCheckingUpdate) return;
            isCheckingUpdate = true;
            Repaint();

            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "UnityVault");

                    // Download catalog from GitHub to get latest version
                    string catalogUrl = $"{GITHUB_RAW_BASE}/catalog.json";
                    string catalogJson = client.DownloadString(catalogUrl);

                    // Parse version from catalog
                    var remoteCatalog = JsonUtility.FromJson<CatalogData>(catalogJson);
                    if (remoteCatalog != null && !string.IsNullOrEmpty(remoteCatalog.version))
                    {
                        latestVersion = remoteCatalog.version;

                        // Compare versions (simple string comparison, assumes semantic versioning)
                        updateAvailable = CompareVersions(remoteCatalog.version, VERSION) > 0;

                        lastUpdateCheck = DateTime.Now.ToString("MMM dd, HH:mm");

                        if (updateAvailable)
                        {
                            EditorUtility.DisplayDialog("Update Available",
                                $"A new version of UnityVault library is available!\n\n" +
                                $"Current: v{VERSION}\n" +
                                $"Latest: v{latestVersion}\n\n" +
                                "Click 'Refresh' in Settings to update the catalog.",
                                "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Up to Date",
                                $"You're running the latest version (v{VERSION}).",
                                "OK");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityVault] Update check failed: {e.Message}");
                EditorUtility.DisplayDialog("Update Check Failed",
                    $"Could not check for updates.\nError: {e.Message}",
                    "OK");
            }
            finally
            {
                isCheckingUpdate = false;
                Repaint();
            }
        }

        private int CompareVersions(string v1, string v2)
        {
            // Compare semantic versions (e.g., "4.0.0" vs "4.1.0")
            try
            {
                var parts1 = v1.Split('.').Select(int.Parse).ToArray();
                var parts2 = v2.Split('.').Select(int.Parse).ToArray();

                for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
                {
                    int p1 = i < parts1.Length ? parts1[i] : 0;
                    int p2 = i < parts2.Length ? parts2[i] : 0;

                    if (p1 != p2)
                        return p1.CompareTo(p2);
                }
                return 0;
            }
            catch
            {
                return string.Compare(v1, v2, StringComparison.Ordinal);
            }
        }

        #endregion

        #region Progress Overlay

        private void DrawProgressOverlay()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0, 0, 0, 0.7f));

            float boxW = 400;
            float boxH = 120;
            float boxX = (position.width - boxW) / 2;
            float boxY = (position.height - boxH) / 2;

            GUI.DrawTexture(new Rect(boxX, boxY, boxW, boxH), textures["bg_card"]);

            string title = isGenerating ? "Generating with AI..." : "Importing Systems...";
            GUI.Label(new Rect(boxX, boxY + 15, boxW, 25), title, new GUIStyle() {
                fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }, alignment = TextAnchor.MiddleCenter
            });

            GUI.Label(new Rect(boxX, boxY + 45, boxW, 20), importStatus, new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_LIGHT }, alignment = TextAnchor.MiddleCenter
            });

            EditorGUI.DrawRect(new Rect(boxX + 30, boxY + 75, boxW - 60, 20), BG_DARK);
            float fillWidth = (boxW - 60) * importProgress;
            EditorGUI.DrawRect(new Rect(boxX + 30, boxY + 75, fillWidth, 20), isGenerating ? ACCENT_PURPLE : ACCENT_GREEN);

            GUI.Label(new Rect(boxX, boxY + 75, boxW, 20), $"{Mathf.RoundToInt(importProgress * 100)}%", new GUIStyle() {
                fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
            });
        }

        #endregion

        #region UI Helpers

        private void DrawBadge(Rect rect, string text, Color color)
        {
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, text, new GUIStyle() {
                fontSize = 9, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
            });
        }

        private bool DrawButton(Rect rect, string text, Color color, bool enabled)
        {
            var oldEnabled = GUI.enabled;
            GUI.enabled = enabled;

            Color btnColor = enabled ? color : new Color(0.3f, 0.3f, 0.3f);
            EditorGUI.DrawRect(rect, btnColor);

            bool result = GUI.Button(rect, "", GUIStyle.none);

            GUI.Label(rect, text, new GUIStyle() {
                fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter
            });

            GUI.enabled = oldEnabled;
            return result && enabled;
        }

        private GUIStyle GetSearchFieldStyle()
        {
            return new GUIStyle(EditorStyles.textField) { fontSize = 11, fixedHeight = 24, padding = new RectOffset(8, 8, 4, 4) };
        }

        private GUIStyle GetIconButtonStyle()
        {
            return new GUIStyle(GUI.skin.button) { fontSize = 14, fixedWidth = 40, fixedHeight = 24 };
        }

        private GUIStyle GetMiniButtonStyle()
        {
            return new GUIStyle(GUI.skin.button) { fontSize = 10 };
        }

        private float CalculateContentHeight()
        {
            if (catalog?.categories == null) return 500;

            float height = detectedConflicts.Count > 0 ? 75 : 20;

            foreach (var category in catalog.categories)
            {
                var systems = GetFilteredSystems(category.id);
                if (systems.Count == 0) continue;

                height += 58;
                if (expandedCategories.ContainsKey(category.id) && expandedCategories[category.id])
                {
                    // Task 4: Use dynamic heights for systems with options
                    foreach (var system in systems)
                    {
                        height += GetSystemItemHeight(system);
                    }
                    height += 10;
                }
            }
            return height + 20;
        }

        private void DrawNoCatalog(float y, float height)
        {
            float centerY = y + height / 2 - 50;

            GUI.Label(new Rect(0, centerY, position.width, 30), "Library Not Found", new GUIStyle() {
                fontSize = 18, fontStyle = FontStyle.Bold, normal = { textColor = TEXT_WHITE }, alignment = TextAnchor.MiddleCenter
            });

            GUI.Label(new Rect(50, centerY + 40, position.width - 100, 40), $"Expected at:\n{libraryPath}", new GUIStyle() {
                fontSize = 11, normal = { textColor = TEXT_GRAY }, alignment = TextAnchor.MiddleCenter, wordWrap = true
            });

            if (DrawButton(new Rect(position.width / 2 - 75, centerY + 100, 150, 36), "Refresh", ACCENT_BLUE, true))
                LoadCatalog();
        }

        #endregion

        #region Data & Logic

        private void LoadCatalog()
        {
            // Try cache first, then local library, then download from GitHub
            string cachedCatalog = Path.Combine(cachePath, "catalog.json");
            string localCatalog = Path.Combine(libraryPath, "catalog.json");

            string catalogContent = null;

            // 1. Try cached catalog (less than 1 day old)
            if (File.Exists(cachedCatalog))
            {
                var cacheAge = DateTime.Now - File.GetLastWriteTime(cachedCatalog);
                if (cacheAge.TotalDays < 1)
                {
                    catalogContent = File.ReadAllText(cachedCatalog);
                }
            }

            // 2. Try local library
            if (catalogContent == null && File.Exists(localCatalog))
            {
                catalogContent = File.ReadAllText(localCatalog);
            }

            // 3. Download from GitHub
            if (catalogContent == null)
            {
                catalogContent = DownloadFromGitHub("catalog.json");
                if (catalogContent != null)
                {
                    File.WriteAllText(cachedCatalog, catalogContent);
                }
            }

            if (catalogContent == null) { catalogLoaded = false; return; }

            try
            {
                catalog = JsonUtility.FromJson<CatalogData>(catalogContent);
                catalogLoaded = catalog != null;

                if (catalog?.categories != null)
                {
                    foreach (var cat in catalog.categories)
                        if (!expandedCategories.ContainsKey(cat.id))
                            expandedCategories[cat.id] = true;
                }

                LayoutGraphNodes();
            }
            catch { catalogLoaded = false; }
        }

        private string DownloadFromGitHub(string relativePath)
        {
            try
            {
                string url = $"{GITHUB_RAW_BASE}/{relativePath}";
                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "UnityVault");
                    return client.DownloadString(url);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityVault] Failed to download {relativePath}: {e.Message}");
                return null;
            }
        }

        private bool DownloadFileFromGitHub(string relativePath, string destPath)
        {
            try
            {
                string url = $"{GITHUB_RAW_BASE}/{relativePath}";
                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "UnityVault");
                    client.DownloadFile(url, destPath);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityVault] Failed to download {relativePath}: {e.Message}");
                return false;
            }
        }

        private void RefreshCatalogFromGitHub()
        {
            string cachedCatalog = Path.Combine(cachePath, "catalog.json");
            string content = DownloadFromGitHub("catalog.json");
            if (content != null)
            {
                File.WriteAllText(cachedCatalog, content);
                LoadCatalog();
                EditorUtility.DisplayDialog("Refresh Complete", "Catalog updated from GitHub!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Refresh Failed", "Could not download catalog from GitHub.", "OK");
            }
        }

        private void LoadUserData()
        {
            var favStr = EditorPrefs.GetString(PREFS_FAVORITES, "");
            if (!string.IsNullOrEmpty(favStr))
                favorites = new HashSet<string>(favStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            var recentStr = EditorPrefs.GetString(PREFS_RECENT, "");
            if (!string.IsNullOrEmpty(recentStr))
                recentImports = recentStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var settingsStr = EditorPrefs.GetString(PREFS_SETTINGS, "");
            if (!string.IsNullOrEmpty(settingsStr))
                settings = JsonUtility.FromJson<UserSettings>(settingsStr) ?? new UserSettings();
        }

        private void SaveUserData()
        {
            EditorPrefs.SetString(PREFS_FAVORITES, string.Join(",", favorites));
            EditorPrefs.SetString(PREFS_RECENT, string.Join(",", recentImports.Take(20)));
            EditorPrefs.SetString(PREFS_SETTINGS, JsonUtility.ToJson(settings));
        }

        private List<SystemData> GetFilteredSystems(string categoryId)
        {
            if (catalog?.systems == null) return new List<SystemData>();

            var systems = catalog.systems.Where(s => s.category == categoryId);

            if (!string.IsNullOrEmpty(searchFilter))
            {
                var search = searchFilter.ToLower();
                systems = systems.Where(s =>
                    s.name.ToLower().Contains(search) ||
                    s.description.ToLower().Contains(search) ||
                    (s.tags != null && s.tags.Any(t => t.ToLower().Contains(search)))
                );
            }

            if (selectedTagFilter != "All")
            {
                systems = systems.Where(s =>
                    (selectedTagFilter == "Essential" && s.priority == "P0") ||
                    (s.tags != null && s.tags.Any(t => t.ToLower().Contains(selectedTagFilter.ToLower()))) ||
                    s.category.ToLower().Contains(selectedTagFilter.ToLower())
                );
            }

            return systems.ToList();
        }

        private void SelectDependencies(string systemId)
        {
            var system = catalog?.systems?.FirstOrDefault(s => s.id == systemId);
            if (system?.dependencies == null) return;

            foreach (var depId in system.dependencies)
            {
                if (!selectedSystems.ContainsKey(depId) || !selectedSystems[depId])
                {
                    selectedSystems[depId] = true;
                    SelectDependencies(depId);
                }
            }
        }

        private void ApplyPreset()
        {
            if (selectedPresetIndex == 0) return;

            foreach (var key in selectedSystems.Keys.ToList())
                selectedSystems[key] = false;

            var template = gameTemplates.Values.ElementAtOrDefault(selectedPresetIndex - 1);
            if (template != null)
            {
                foreach (var sysId in template.systems)
                {
                    selectedSystems[sysId] = true;
                    if (settings.autoSelectDependencies)
                        SelectDependencies(sysId);
                }
            }

            DetectConflicts();
        }

        private void ClearAllSelections()
        {
            foreach (var key in selectedSystems.Keys.ToList())
                selectedSystems[key] = false;

            previewSystemId = null;
            livePreviewCode = null;
            DetectConflicts();
            Repaint();
        }

        private bool IsSystemInLibrary(string systemId)
        {
            var system = catalog?.systems?.FirstOrDefault(s => s.id == systemId);
            if (system == null) return false;

            // Check local library
            string localPath = Path.Combine(libraryPath, system.path);
            if (Directory.Exists(localPath))
            {
                var files = Directory.GetFiles(localPath, "*.cs");
                if (files.Length > 0) return true;
            }

            // Check cache
            string cacheFolderPath = Path.Combine(cachePath, system.path);
            if (Directory.Exists(cacheFolderPath))
            {
                var files = Directory.GetFiles(cacheFolderPath, "*.cs");
                if (files.Length > 0) return true;
            }

            return false;
        }

        private bool IsSystemInProject(string systemId)
        {
            var system = catalog?.systems?.FirstOrDefault(s => s.id == systemId);
            if (system == null) return false;
            return Directory.Exists(Path.Combine(Application.dataPath, "UnityVault/Generated", Path.GetFileName(system.path)));
        }

        private void ImportSingleSystem(SystemData system)
        {
            EnsureFolder("Assets/UnityVault/Generated");

            string destPath = Path.Combine(Application.dataPath, "UnityVault/Generated", Path.GetFileName(system.path));
            if (!Directory.Exists(destPath))
                Directory.CreateDirectory(destPath);

            // Try sources in order: local > cache > GitHub
            bool success = ImportSystemFromSource(system, destPath);

            if (success)
            {
                recentImports.Remove(system.id);
                recentImports.Insert(0, system.id);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Success", $"Imported: {system.name}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Failed to import: {system.name}", "OK");
            }
        }

        private void ImportSelectedSystems()
        {
            var selected = selectedSystems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            if (selected.Count == 0) return;

            isImporting = true;
            importProgress = 0f;

            EnsureFolder("Assets/UnityVault/Generated");

            int imported = 0;
            int total = selected.Count;

            foreach (var systemId in selected)
            {
                var system = catalog?.systems?.FirstOrDefault(s => s.id == systemId);
                if (system == null) continue;

                importStatus = $"Importing: {system.name}";
                importProgress = (float)imported / total;
                Repaint();

                string destPath = Path.Combine(Application.dataPath, "UnityVault/Generated", Path.GetFileName(system.path));
                if (!Directory.Exists(destPath))
                    Directory.CreateDirectory(destPath);

                // Try sources in order: local library > cache > GitHub
                bool success = ImportSystemFromSource(system, destPath);

                if (success)
                {
                    recentImports.Remove(systemId);
                    recentImports.Insert(0, systemId);
                    imported++;
                }
            }

            importProgress = 1f;
            isImporting = false;
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Import Complete", $"Imported {imported} systems!", "OK");
        }

        private bool ImportSystemFromSource(SystemData system, string destPath)
        {
            bool success = false;

            // 1. Try local library first
            string localPath = Path.Combine(libraryPath, system.path);
            if (Directory.Exists(localPath))
            {
                var files = Directory.GetFiles(localPath, "*.cs");
                if (files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        string destFile = Path.Combine(destPath, Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                        ApplyCustomNamespace(destFile);
                    }
                    success = true;
                }
            }

            // 2. Try cache
            if (!success)
            {
                string cachedPath = Path.Combine(cachePath, system.path);
                if (Directory.Exists(cachedPath))
                {
                    var files = Directory.GetFiles(cachedPath, "*.cs");
                    if (files.Length > 0)
                    {
                        foreach (var file in files)
                        {
                            string destFile = Path.Combine(destPath, Path.GetFileName(file));
                            File.Copy(file, destFile, true);
                            ApplyCustomNamespace(destFile);
                        }
                        success = true;
                    }
                }
            }

            // 3. Download from GitHub
            if (!success)
            {
                success = DownloadSystemFromGitHub(system, destPath);
            }

            return success;
        }

        private void ApplyCustomNamespace(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                string content = File.ReadAllText(filePath);

                // Apply custom namespace if set
                if (!string.IsNullOrEmpty(settings.customNamespace))
                {
                    // Replace namespace UnityVault.* with custom namespace
                    content = Regex.Replace(content,
                        @"namespace\s+UnityVault\.(\w+)",
                        $"namespace {settings.customNamespace}.$1");

                    // Also handle simple namespace UnityVault
                    content = Regex.Replace(content,
                        @"namespace\s+UnityVault\s*\{",
                        $"namespace {settings.customNamespace} {{");

                    // Update using statements if they reference UnityVault
                    content = Regex.Replace(content,
                        @"using\s+UnityVault\.(\w+)",
                        $"using {settings.customNamespace}.$1");
                }

                // Apply Code Customizer settings
                content = ApplyCodeCustomizations(content, filePath);

                File.WriteAllText(filePath, content);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityVault] Failed to apply customizations to {filePath}: {e.Message}");
            }
        }

        private string ApplyCodeCustomizations(string content, string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            // Add header comment
            if (settings.addHeaderComment)
            {
                string author = string.IsNullOrEmpty(settings.authorName) ? "UnityVault" : settings.authorName;
                string header = $@"// ═══════════════════════════════════════════════════════════════════
// {fileName}
// Generated by UnityVault on {DateTime.Now:yyyy-MM-dd HH:mm}
// Author: {author}
// ═══════════════════════════════════════════════════════════════════

";
                // Only add if not already present
                if (!content.StartsWith("// ═══"))
                    content = header + content;
            }

            // Strip comments if enabled
            if (settings.stripComments)
            {
                // Remove single-line comments (but not header)
                content = Regex.Replace(content, @"^\s*//(?!═).*$", "", RegexOptions.Multiline);
                // Remove multi-line comments
                content = Regex.Replace(content, @"/\*[\s\S]*?\*/", "");
                // Clean up multiple empty lines
                content = Regex.Replace(content, @"\n{3,}", "\n\n");
            }

            // Add regions if enabled
            if (settings.addRegions)
            {
                // Add region around Fields
                content = Regex.Replace(content,
                    @"(public class \w+[^{]*\{)(\s*)((?:\s*(?:public|private|protected|internal|static|readonly|const)\s+\w+[^;{]*;)+)",
                    "$1$2\n        #region Fields$2$3\n        #endregion\n",
                    RegexOptions.Singleline);

                // Add region around Unity callbacks
                content = Regex.Replace(content,
                    @"((?:private|protected|public)?\s*void\s+(?:Awake|Start|Update|FixedUpdate|LateUpdate|OnEnable|OnDisable|OnDestroy)\s*\([^)]*\)\s*\{[^}]*\})+",
                    "\n        #region Unity Lifecycle\n$0\n        #endregion\n");
            }

            return content;
        }

        private bool DownloadSystemFromGitHub(SystemData system, string destPath)
        {
            if (system.files == null || system.files.Length == 0)
            {
                Debug.LogWarning($"[UnityVault] No files defined for system: {system.name}");
                return false;
            }

            // Ensure cache folder for this system
            string systemCachePath = Path.Combine(cachePath, system.path);
            if (!Directory.Exists(systemCachePath))
                Directory.CreateDirectory(systemCachePath);

            bool allSuccess = true;
            foreach (var fileName in system.files)
            {
                string relativePath = $"{system.path}/{fileName}";
                string cacheFilePath = Path.Combine(systemCachePath, fileName);
                string destFilePath = Path.Combine(destPath, fileName);

                importStatus = $"Downloading: {system.name}/{fileName}";
                Repaint();

                // Download to cache
                if (DownloadFileFromGitHub(relativePath, cacheFilePath))
                {
                    // Copy from cache to project
                    File.Copy(cacheFilePath, destFilePath, true);
                    // Apply custom namespace if set
                    ApplyCustomNamespace(destFilePath);
                }
                else
                {
                    allSuccess = false;
                    Debug.LogError($"[UnityVault] Failed to download: {relativePath}");
                }
            }

            // If download failed and Claude API is configured, try AI generation
            if (!allSuccess && !string.IsNullOrEmpty(settings.claudeApiKey))
            {
                Debug.Log($"[UnityVault] Attempting AI generation for: {system.name}");
                allSuccess = GenerateWithClaude(system, destPath);
            }

            return allSuccess;
        }

        private bool GenerateWithClaude(SystemData system, string destPath)
        {
            try
            {
                importStatus = $"AI Generating: {system.name}";
                isGenerating = true;
                Repaint();

                string prompt = BuildClaudePrompt(system);

                using (var client = new WebClient())
                {
                    client.Headers.Add("x-api-key", settings.claudeApiKey);
                    client.Headers.Add("anthropic-version", "2023-06-01");
                    client.Headers.Add("Content-Type", "application/json");

                    var requestData = new
                    {
                        model = "claude-sonnet-4-20250514",
                        max_tokens = 8000,
                        messages = new[]
                        {
                            new { role = "user", content = prompt }
                        }
                    };

                    string jsonRequest = JsonUtility.ToJson(requestData);

                    // Use simple JSON construction since JsonUtility doesn't handle anonymous types well
                    jsonRequest = $@"{{
                        ""model"": ""claude-sonnet-4-20250514"",
                        ""max_tokens"": 8000,
                        ""messages"": [
                            {{""role"": ""user"", ""content"": {JsonUtility.ToJson(prompt)}}}
                        ]
                    }}";

                    string response = client.UploadString("https://api.anthropic.com/v1/messages", jsonRequest);

                    // Parse response to extract code
                    string generatedCode = ParseClaudeResponse(response);

                    if (!string.IsNullOrEmpty(generatedCode))
                    {
                        // Save generated code
                        string fileName = $"{system.name.Replace(" ", "")}.cs";
                        string filePath = Path.Combine(destPath, fileName);
                        File.WriteAllText(filePath, generatedCode);
                        ApplyCustomNamespace(filePath);

                        Debug.Log($"[UnityVault] AI generated: {fileName}");
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UnityVault] AI generation failed: {e.Message}");
            }
            finally
            {
                isGenerating = false;
            }

            return false;
        }

        private string BuildClaudePrompt(SystemData system)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generate a production-ready Unity C# script for: {system.name}");
            sb.AppendLine();
            sb.AppendLine($"Description: {system.description}");
            sb.AppendLine($"Category: {system.category}");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Use namespace: UnityVault." + system.category);
            sb.AppendLine("- Include proper Unity using statements");
            sb.AppendLine("- Add [System.Serializable] where appropriate");
            sb.AppendLine("- Include XML documentation comments");
            sb.AppendLine("- Make it modular and easy to extend");
            sb.AppendLine("- Follow Unity best practices");
            sb.AppendLine();

            if (system.dependencies != null && system.dependencies.Length > 0)
            {
                sb.AppendLine($"Dependencies: {string.Join(", ", system.dependencies)}");
                sb.AppendLine("(These may be required, design to work with or without them)");
            }

            sb.AppendLine();
            sb.AppendLine("Return ONLY the C# code, no markdown formatting or explanations.");

            return sb.ToString();
        }

        private string ParseClaudeResponse(string jsonResponse)
        {
            try
            {
                // Simple JSON parsing to extract content
                // Look for "text" field in the response
                int textStart = jsonResponse.IndexOf("\"text\":\"") + 8;
                if (textStart < 8) return null;

                int textEnd = jsonResponse.IndexOf("\"", textStart);
                while (textEnd > 0 && jsonResponse[textEnd - 1] == '\\')
                {
                    textEnd = jsonResponse.IndexOf("\"", textEnd + 1);
                }

                if (textEnd <= textStart) return null;

                string content = jsonResponse.Substring(textStart, textEnd - textStart);
                // Unescape
                content = content.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

                // Clean up code block markers if present
                content = Regex.Replace(content, @"^```(?:csharp|cs)?\s*", "", RegexOptions.Multiline);
                content = Regex.Replace(content, @"```\s*$", "", RegexOptions.Multiline);

                return content.Trim();
            }
            catch
            {
                return null;
            }
        }

        private void ExportSelectedSystems()
        {
            var selected = selectedSystems.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();
            if (selected.Count == 0) return;

            string path = EditorUtility.SaveFilePanel("Export", "", "UnityVault-Components.unitypackage", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;

            var assetPaths = new List<string>();
            foreach (var systemId in selected)
            {
                var system = catalog?.systems?.FirstOrDefault(s => s.id == systemId);
                if (system == null) continue;

                string folder = $"Assets/UnityVault/Generated/{Path.GetFileName(system.path)}";
                if (AssetDatabase.IsValidFolder(folder))
                    assetPaths.Add(folder);
            }

            if (assetPaths.Count > 0)
            {
                AssetDatabase.ExportPackage(assetPaths.ToArray(), path, ExportPackageOptions.Recurse);
                EditorUtility.DisplayDialog("Export Complete", $"Exported {assetPaths.Count} systems!", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Export Failed", "No imported systems found!", "OK");
            }
        }

        private void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Task 4: MCP connection check
        private bool CheckMCPConnection()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect("localhost", MCP_PORT, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));

                    if (success && client.Connected)
                    {
                        client.EndConnect(result);
                        isMCPConnected = true;
                        return true;
                    }
                }
            }
            catch { }

            isMCPConnected = false;
            return false;
        }

        // Task 4: Get system option selections with defaults
        private Dictionary<string, bool> GetSystemOptions(string systemId)
        {
            if (!systemOptionSelections.ContainsKey(systemId))
            {
                systemOptionSelections[systemId] = new Dictionary<string, bool>();

                var system = catalog?.systems?.FirstOrDefault(s => s.id == systemId);
                if (system?.options != null)
                {
                    foreach (var opt in system.options)
                    {
                        systemOptionSelections[systemId][opt.id] = opt.defaultValue;
                    }
                }
            }
            return systemOptionSelections[systemId];
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
        }

        [Serializable]
        public class CategoryData
        {
            public string id;
            public string name;
            public string icon;
            public string description;
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
            public SystemOption[] options; // Task 4: Optional system configurations
        }

        [Serializable]
        public class SystemOption
        {
            public string id;
            public string name;
            public string description;
            public bool defaultValue;
        }

        [Serializable]
        public class UserSettings
        {
            public bool autoSelectDependencies = true;
            public bool showConflictWarnings = true;
            public bool confirmBeforeImport = false;
            public string githubToken = "";
            public string gistId = "";
            public string customNamespace = "";
            public string claudeApiKey = "";
            public bool autoCheckUpdates = true;

            // Code Customizer options
            public bool addHeaderComment = true;
            public bool addRegions = false;
            public bool stripComments = false;
            public string authorName = "";
        }

        public class GameTemplate
        {
            public string name;
            public string description;
            public string[] systems;
            public Color color;

            public GameTemplate(string name, string desc, string[] systems, Color color)
            {
                this.name = name;
                this.description = desc;
                this.systems = systems;
                this.color = color;
            }
        }

        public class SceneTemplate
        {
            public string name;
            public string description;
            public string[] objects;
            public string[] requiredSystems;
            public Color color;

            public SceneTemplate(string name, string desc, string[] objects, string[] systems, Color color)
            {
                this.name = name;
                this.description = desc;
                this.objects = objects;
                this.requiredSystems = systems;
                this.color = color;
            }
        }

        public class ConflictInfo
        {
            public string systemId;
            public string systemName;
            public string conflictType;
            public string details;
        }

        public class ProjectAnalysis
        {
            public int totalScripts;
            public int totalScenes;
            public List<string> installedSystems = new List<string>();
            public List<string> suggestedSystems = new List<string>();
            public List<string> detectedPatterns = new List<string>();
            public Dictionary<string, int> categoryUsage = new Dictionary<string, int>();
            public string projectType = "Unknown";
            public DateTime analysisTime;
        }

        public class ChatMessage
        {
            public bool isUser;
            public string content;
            public string codeBlock;
            public DateTime timestamp;

            public ChatMessage(bool isUser, string content, string codeBlock = null)
            {
                this.isUser = isUser;
                this.content = content;
                this.codeBlock = codeBlock;
                this.timestamp = DateTime.Now;
            }
        }

        #endregion
    }
}
