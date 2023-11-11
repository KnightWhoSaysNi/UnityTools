using System.IO;
using UnityEditor;
using UnityEngine;
using static System.IO.Directory;

namespace Ni.Tools
{
    public static class FolderOrganization
    {
        [MenuItem("Tools/Project Setup/Create Default Folders"), Tooltip("Doesn't remove existing folders.")]
        private static void CreateDefaultFolders()
        {
            CreateDirectories("Art", "Animation", "Models", "Materials", "Textures");
            CreateDirectories("Audio", "Music", "Sounds");
            CreateDirectories("Code", "Scripts", "Shaders", "Editor");
            CreateDirectories("Prefabs");
            CreateDirectories("Scenes");
            CreateDirectories("Presets");
            CreateDirectories("ImportedAssets");

            AssetDatabase.Refresh();
        }

        private static void CreateDirectories(string folder, params string[] subfolders)
        {
            string fullpath = Path.Combine(Application.dataPath, folder);
            CreateDirectory(fullpath);

            foreach (var subfolder in subfolders)
            {
                string subfolderPath = Path.Combine(folder, subfolder);
                CreateDirectories(subfolderPath);
            }
        }
    }
}
