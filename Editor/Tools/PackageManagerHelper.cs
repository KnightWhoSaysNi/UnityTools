using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ni.Tools
{
    public class PackageManagerHelper : EditorWindow
    {
        #region - Fields -
        // https://gist.github.com/KnightWhoSaysNi/68ad5a0baafe72ead567d29e15f37784
        private readonly string gistId = "68ad5a0baafe72ead567d29e15f37784";

        private List<string> allPackageNames = new List<string>();
        private List<string> installedPackageNames = new List<string>();
        private List<string> availablePackageNames = new List<string>();

        private Dictionary<string, bool> installedPackages = new Dictionary<string, bool>();
        private Dictionary<string, bool> availablePackages = new Dictionary<string, bool>();

        /// <summary>
        /// Request for all installed packages.
        /// </summary>
        private ListRequest listRequest;
        private AddAndRemoveRequest addAndRemoveRequest;
        private bool areAllPackageNamesGenerated;
        private bool isWaitingForPackageUpdates;
        private bool isWaitingForListRequest;
        private bool isListRequested;

        // Used for scroll views
        private Vector2 installedPackagesScrollPosition = new Vector2(1, 1);
        private Vector2 availablePackagesScrollPosition = new Vector2(1, 1); 
        #endregion

        [MenuItem("Tools/Project Setup/Package Manager Helper"), Tooltip("A window with options to remove/add multliple packages at the same time.")]
        private static void OpenWindow()
        {
            PackageManagerHelper packageManagerHelper = GetWindow<PackageManagerHelper>("Package Manager Helper");
            packageManagerHelper.Focus();
        }

        #region - OnEnable/OnDisable -
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // After PackageManager updates packages this gets called again, even though the window never closed.
        // This happens MOST OF THE TIME. Sometimes it doesn't...
        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate; // just in case. Unity is weird
            EditorApplication.update += OnEditorUpdate;

            if (!isListRequested)
            {
                listRequest = Client.List();
                isWaitingForListRequest = true;
                isListRequested = true;
            }
            if (allPackageNames.Count == 0)
            {
                GeneratePackageNamesFromGithubGist(gistId);
            }
        }
        // Must use OnDestroy instead of OnDisable as, for some reason, OnDisable gets called right after the PackageManager updates packages
        private void OnDestroy()
        {
            EditorApplication.update -= OnEditorUpdate;

            ResetVariables();
        }
        private void ResetVariables()
        {
            listRequest = null;
            addAndRemoveRequest = null;

            allPackageNames.Clear();
            installedPackageNames.Clear();
            availablePackageNames.Clear();
            installedPackages.Clear();
            availablePackages.Clear();

            areAllPackageNamesGenerated = false;
            isWaitingForListRequest = false;
            isWaitingForPackageUpdates = false;
        } 
        #endregion

        #region - Get all package names from a github gist -
        /// <summary>
        /// Generates names of all Unity packages, from the provided github gist.
        /// </summary>
        private async void GeneratePackageNamesFromGithubGist(string id, string user = "KnightWhoSaysNi")
        {
            areAllPackageNamesGenerated = false;
            string gistUrl = $"https://gist.githubusercontent.com/{user}/{id}/raw";
            string contents = await GetContents(gistUrl);

            PackageList packageList = new PackageList();
            JsonUtility.FromJsonOverwrite(contents, packageList);

            foreach (string package in packageList.packages)
            {
                allPackageNames.Add(package);
            }
            areAllPackageNamesGenerated = true;
        }
        private async Task<string> GetContents(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(url);
                string content = await response.Content.ReadAsStringAsync();
                return content;
            }
        }

        /// <summary>
        /// Wrapper class for a .json array file.
        /// </summary>
        [System.Serializable]
        private class PackageList
        {
            public List<string> packages;
        }
        #endregion

        #region - Generate package collections -
        private void OnEditorUpdate()
        {
            if (listRequest != null && isWaitingForListRequest)
            {
                GeneratePackageCollections();
            }

            if (addAndRemoveRequest != null && isWaitingForPackageUpdates)
            {
                ResolveAddAndRemoveRequest();
            }
        }

        /// <summary>
        /// Generates collections of intalled and not yet installed Unity packages.
        /// </summary>
        private void GeneratePackageCollections()
        {
            if (!listRequest.IsCompleted)
            {
                return;
            }

            if (listRequest.Status == StatusCode.Failure)
            {
                string message = listRequest.Error == null ? "Something went wrong. Please reopen the window." : listRequest.Error.message;
                Debug.LogError(message);
                listRequest = null;
                isListRequested = false;
                Close();
                return;
            }
            // else
            // ListRequest successfully returned the list of installed packages

            if (installedPackages.Count == 0)
            {
                foreach (var package in listRequest.Result)
                {
                    installedPackageNames.Add(package.name);
                    installedPackages.Add(package.name, true);
                }
            }

            if (installedPackages.Count > 0 && areAllPackageNamesGenerated)
            {
                // Fill up a dictionary of not yet installed - available packages
                foreach (var packageName in allPackageNames)
                {
                    if (!installedPackageNames.Contains(packageName))
                    {
                        availablePackageNames.Add(packageName);
                        availablePackages.Add(packageName, false);
                    }
                }

                // Stop OnEditorUpdate going into this method
                Repaint();
                listRequest = null;
                isListRequested = false;
                isWaitingForListRequest = false;
            }
        }
        private void ResolveAddAndRemoveRequest()
        {
            if (!addAndRemoveRequest.IsCompleted)
            {
                return;
            }

            if (addAndRemoveRequest.Status == StatusCode.Failure)
            {
                Debug.LogError(addAndRemoveRequest.Error?.message);
            }
            else
            {
                Debug.Log("Packages updated successfully.");
            }

            addAndRemoveRequest = null;
            isWaitingForPackageUpdates = false;
            listRequest = Client.List();
            isWaitingForListRequest = true;
            isListRequested = true;
        }
        #endregion

        #region - OnGUI stuff -
        private void OnGUI()
        {
            if (isWaitingForPackageUpdates)
            {
                GUILayout.Label("Updating packages...", EditorStyles.wordWrappedLabel);
                GUILayout.Label("This might take a while to get started. Please don't add/remove packages from PackageManager manually until this operation is finished.", EditorStyles.wordWrappedLabel);
            }
            else if (isWaitingForListRequest || availablePackages.Count == 0)
            {
                GUILayout.Label("Fetching packages...");
            }
            else // Packages are ready to be displayed
            {
                using (new GUILayout.VerticalScope())
                {
                    using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        DisplayPackagesForRemoval();
                        DisplayPackagesForAddition();
                    }
                    GUILayout.Space(5);
                    DisplayFinalizeButton();
                }
            }
        }

        private void DisplayPackagesForRemoval()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Select packages you wish to remove:", EditorStyles.boldLabel);
                GUILayout.Space(10);

                installedPackagesScrollPosition = EditorGUILayout.BeginScrollView(installedPackagesScrollPosition);
                foreach (var package in installedPackageNames)
                {
                    installedPackages[package] = GUILayout.Toggle(installedPackages[package], package);
                }
                EditorGUILayout.EndScrollView();
            }
        }
        private void DisplayPackagesForAddition()
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Select packages you wish to add:", EditorStyles.boldLabel);
                GUILayout.Space(10);

                availablePackagesScrollPosition = EditorGUILayout.BeginScrollView(availablePackagesScrollPosition);
                foreach (var packageName in availablePackageNames)
                {
                    availablePackages[packageName] = GUILayout.Toggle(availablePackages[packageName], packageName);
                }
                EditorGUILayout.EndScrollView();
            }
            GUILayout.Space(5);
        }

        private void DisplayFinalizeButton()
        {
            GUILayout.Space(30);

            if (GUILayout.Button("Finalize selected package removal and addition", GUILayout.Width(300), GUILayout.Height(50)))
            {
                List<string> packagesToAdd = new List<string>();
                foreach (var packageName in availablePackages.Keys)
                {
                    if (availablePackages[packageName] == true)
                    {
                        packagesToAdd.Add(packageName);
                    }
                }

                List<string> packagesToRemove = new List<string>();
                foreach (var package in installedPackageNames)
                {
                    if (installedPackages[package] == false)
                    {
                        packagesToRemove.Add(package);
                    }
                }

                if (packagesToAdd.Count == 0 && packagesToRemove.Count == 0)
                {
                    GUILayout.Space(10);
                    return;
                }

                installedPackageNames.Clear();
                availablePackageNames.Clear();
                installedPackages.Clear();
                availablePackages.Clear();
                listRequest = null;

                try
                {
                    isWaitingForPackageUpdates = true;
                    addAndRemoveRequest = Client.AddAndRemove(
                        packagesToAdd.Count > 0 ? packagesToAdd.ToArray() : null,
                        packagesToRemove.Count > 0 ? packagesToRemove.ToArray() : null);
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);

                }
            }

            GUILayout.Space(10);
        } 
        #endregion
    } 
}
