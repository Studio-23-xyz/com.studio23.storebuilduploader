using UnityEngine;

[CreateAssetMenu(fileName = "SteamUploadConfig", menuName = "Steam/UploadConfig")]
public class SteamUploadConfig : ScriptableObject
{
    public string steamSDKPath;
    public string buildFolderPath;
    public string appId;
    public string releaseBranch;
    public string username;
    public string password;  // Add these fields for username and password
}