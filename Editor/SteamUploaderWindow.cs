using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class SteamUploaderWindow : EditorWindow
{
    private SteamUploadConfig config;
    private string errorMessage;
    private const string configFolderPath = "Assets/Resources/StoreBuildUploader/";
    private const string configFileName = "SteamUploadConfig.asset";
    private const string vdfFileNameTemplate = "app_build_{0}.vdf";

    [MenuItem("Studio-23/Store Build Uploader/Steam Uploader")]
    public static void ShowWindow()
    {
        GetWindow<SteamUploaderWindow>("Steam Upload");
    }

    private void OnEnable()
    {
        LoadOrCreateConfig();
    }

    private void OnGUI()
    {
        config = (SteamUploadConfig)EditorGUILayout.ObjectField("Config", config, typeof(SteamUploadConfig), false);

        if (config != null)
        {
            // Steamworks SDK Path
            EditorGUILayout.BeginHorizontal();
            config.steamSDKPath = EditorGUILayout.TextField("Steamworks SDK Path", config.steamSDKPath);
            if (GUILayout.Button("Browse", GUILayout.Width(75)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Steamworks SDK Path", config.steamSDKPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    if (ValidateSDKPath(selectedPath))
                    {
                        config.steamSDKPath = selectedPath;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            // Build Folder Path
            EditorGUILayout.BeginHorizontal();
            config.buildFolderPath = EditorGUILayout.TextField("Build Folder Path", config.buildFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(75)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("Select Build Folder Path", config.buildFolderPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    config.buildFolderPath = selectedPath;
                }
            }
            EditorGUILayout.EndHorizontal();

            // App ID and Release Branch
            config.appId = EditorGUILayout.TextField("App ID", config.appId);
            config.releaseBranch = EditorGUILayout.TextField("Release Branch", config.releaseBranch);

            // Username and Password
            config.username = EditorGUILayout.TextField("Username", config.username);
            config.password = EditorGUILayout.PasswordField("Password", config.password);

            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
            }

            if (GUILayout.Button("Copy Content to Steamworks SDK Folder"))
            {
                CopyContentToSteamworksFolder(config);
                OpenSteamworksContentFolder(config);
            }

            if (GUILayout.Button("Update app_build.vdf File from Configurations"))
            {
                GenerateVdfFile(config);
            }

            if (GUILayout.Button("Authenticate SteamCMD"))
            {
                RunSteamCMD();
            }

            if (GUILayout.Button("Update run_build.bat"))
            {
                UpdateRunBuildBat(config);
            }

            if (GUILayout.Button("Upload to Steam"))
            {
                UploadToSteam(config);
                
            }
        }
    }



    private void CopyContentToSteamworksFolder(SteamUploadConfig config)
    {
        string contentFolderPath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/content");

        try
        {
            // Clear the content folder
            if (Directory.Exists(contentFolderPath))
            {
                Directory.Delete(contentFolderPath, true);
            }

            Directory.CreateDirectory(contentFolderPath);

            // Copy all content from build folder to content folder
            CopyAll(new DirectoryInfo(config.buildFolderPath), new DirectoryInfo(contentFolderPath));

            Debug.Log("Content copied to Steamworks SDK content folder successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to copy content: " + e.Message);
        }
    }

    private void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        Directory.CreateDirectory(target.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo file in source.GetFiles())
        {
            file.CopyTo(Path.Combine(target.FullName, file.Name), true);
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo subdir in source.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(subdir.Name);
            CopyAll(subdir, nextTargetSubDir);
        }
    }


    private void LoadOrCreateConfig()
    {
        config = Resources.Load<SteamUploadConfig>("StoreBuildUploader/SteamUploadConfig");
        if (config == null)
        {
            if (!Directory.Exists(configFolderPath))
            {
                Directory.CreateDirectory(configFolderPath);
            }

            config = CreateInstance<SteamUploadConfig>();
            AssetDatabase.CreateAsset(config, configFolderPath + configFileName);
            AssetDatabase.SaveAssets();

            Debug.Log("Created new SteamUploadConfig at " + configFolderPath + configFileName);
        }
    }

    private bool ValidateSDKPath(string sdkPath)
    {
        string builderPath = Path.Combine(sdkPath, "tools/ContentBuilder/builder/steamcmd.exe");
        string vdfPath = Path.Combine(sdkPath, "tools/ContentBuilder/scripts/", string.Format(vdfFileNameTemplate, config.appId));

        if (!File.Exists(builderPath))
        {
            errorMessage = "Invalid SDK Path: steamcmd.exe not found in the specified Steamworks SDK path.";
            Debug.LogError(errorMessage);
            return false;
        }

        

        errorMessage = string.Empty;
        return true;
    }

    private void GenerateVdfFile(SteamUploadConfig config)
    {
        string vdfContent = GenerateVdfContent(config);
        string vdfPath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/scripts/", string.Format(vdfFileNameTemplate, config.appId));

        try
        {
            File.WriteAllText(vdfPath, vdfContent);
            Debug.Log("VDF file generated at: " + vdfPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to generate VDF file: " + e.Message);
        }
    }

    private string GenerateVdfContent(SteamUploadConfig config)
    {
        int depotId = int.Parse(config.appId) + 1;

        return $"\"AppBuild\"\n" +
               $"{{\n" +
               $"\t\"AppID\" \"{config.appId}\"\n" +
               $"\t\"Desc\" \"Build uploaded via Unity plugin\"\n" +
               $"\t\"BuildOutput\" \"..\\output\\\"\n" +
               $"\t\"ContentRoot\" \"..\\content\\\"\n" +
               $"\t\"SetLive\" \"{config.releaseBranch}\"\n" +
               $"\t\"Depots\"\n" +
               $"\t{{\n" +
               $"\t\t\"{depotId}\"\n" +
               $"\t\t{{\n" +
               $"\t\t\t\"FileMapping\"\n" +
               $"\t\t\t{{\n" +
               $"\t\t\t\t\"LocalPath\" \"*\"\n" +
               $"\t\t\t\t\"DepotPath\" \".\"\n" +
               $"\t\t\t\t\"recursive\" \"1\"\n" +
               $"\t\t\t}}\n" +
               $"\t\t}}\n" +
               $"\t}}\n" +
               $"}}";
    }

    private void OpenSteamworksContentFolder(SteamUploadConfig config)
    {
        string contentFolderPath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/content");

        if (Directory.Exists(contentFolderPath))
        {
            contentFolderPath = contentFolderPath.Replace(@"/", @"\"); // Replaces slashes with backslashes
            Process.Start("explorer.exe", "/select," + contentFolderPath); // Opens file explorer with the specified path selected
        }
        else
        {
            Debug.LogError("Content folder does not exist: " + contentFolderPath);
        }
    }


    private void UpdateRunBuildBat(SteamUploadConfig config)
    {
        string batchFilePath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/run_build.bat");
        string vdfFilePath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/scripts/", $"app_build_{config.appId}.vdf");

        if (!File.Exists(batchFilePath))
        {
            Debug.LogError("run_build.bat not found: " + batchFilePath);
            return;
        }

        string batchFileContent = $"@echo off\n" +
                                  $"cd /d \"{config.steamSDKPath}\\tools\\ContentBuilder\\builder\"\n" +
                                  $"steamcmd.exe +login {config.username} +run_app_build \"{vdfFilePath}\" +quit\n";

        try
        {
            File.WriteAllText(batchFilePath, batchFileContent);
            Debug.Log("run_build.bat updated successfully.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to update run_build.bat: " + e.Message);
        }
    }

    private void RunSteamCMD()
    {
        string steamCmdPath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/builder/steamcmd.exe");

        if (!File.Exists(steamCmdPath))
        {
            Debug.LogError("SteamCMD not found: " + steamCmdPath);
            return;
        }

        try
        {
            Process steamCmdProcess = new Process();
            steamCmdProcess.StartInfo.FileName = steamCmdPath;
            steamCmdProcess.StartInfo.UseShellExecute = true;  // Use shell execute to allow full interaction
            steamCmdProcess.StartInfo.CreateNoWindow = false;  // Show the command prompt window
            steamCmdProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

            steamCmdProcess.Start();
            steamCmdProcess.WaitForExit();

            if (steamCmdProcess.ExitCode != 0)
            {
                Debug.LogError("SteamCMD process exited with code: " + steamCmdProcess.ExitCode);
            }
            else
            {
                Debug.Log("SteamCMD process completed successfully.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to start SteamCMD process: " + e.Message);
        }
    }





    private void UploadToSteam(SteamUploadConfig config)
    {
        string batchFilePath = Path.Combine(config.steamSDKPath, "tools/ContentBuilder/run_build.bat");

        if (!File.Exists(batchFilePath))
        {
            Debug.LogError("run_build.bat not found: " + batchFilePath);
            return;
        }

        try
        {
            Process batchProcess = new Process();
            batchProcess.StartInfo.FileName = batchFilePath;
            batchProcess.StartInfo.UseShellExecute = true;  // UseShellExecute to run the batch file normally
            batchProcess.StartInfo.CreateNoWindow = false;  // Show the command prompt window
            batchProcess.Start();

            batchProcess.WaitForExit();

            if (batchProcess.ExitCode != 0)
            {
                Debug.LogError("run_build.bat process exited with code: " + batchProcess.ExitCode);
            }
            else
            {
                Debug.Log("run_build.bat process completed successfully.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to start run_build.bat process: " + e.Message);
        }
    }
}
