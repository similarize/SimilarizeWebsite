using UnityEngine;

// ffu34: every save in the game goes through one small interface, so a console port can swap PlayerPrefs for the
// platform save API (Xbox GDK connected storage, PlayStation save data) in one place. Default = PlayerPrefs, which is
// exactly what the game used before (same keys, same values), so nothing changes for WebGL players' existing saves.
public interface ISaveStore
{
    int GetInt(string key, int def);
    void SetInt(string key, int value);
    string GetString(string key, string def);
    void SetString(string key, string value);
    void Flush();
}

public class PlayerPrefsSaveStore : ISaveStore
{
    public int GetInt(string key, int def) { return PlayerPrefs.GetInt(key, def); }
    public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); }
    public string GetString(string key, string def) { return PlayerPrefs.GetString(key, def); }
    public void SetString(string key, string value) { PlayerPrefs.SetString(key, value); }
    public void Flush() { PlayerPrefs.Save(); }
}

public static class SaveStore
{
    static ISaveStore store;
    // replace before anything loads (e.g. from a platform bootstrap) to change the backend
    public static ISaveStore Store { get { if (store == null) store = new PlayerPrefsSaveStore(); return store; } set { store = value; } }
    public static int GetInt(string key, int def = 0) { return Store.GetInt(key, def); }
    public static void SetInt(string key, int value) { Store.SetInt(key, value); }
    public static string GetString(string key, string def = "") { return Store.GetString(key, def); }
    public static void SetString(string key, string value) { Store.SetString(key, value); }
    public static void Flush() { Store.Flush(); }
}
