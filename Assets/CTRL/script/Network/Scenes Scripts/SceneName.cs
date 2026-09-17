public static class SceneName
{
    public const string _MAIN_MENU = "MainMenu_Test";
    public const string _SHORT_MAP_1 = "ShortMap1";
    public const string _LONG_MAP_1 = "LongMap1";
    
    
    public static string GetPath(string sceneName)
    {
        switch (sceneName)
        {
            case _MAIN_MENU:
                return _MAIN_MENU;
            case _SHORT_MAP_1:
                return $"Assets/Scenes/Setup/{_SHORT_MAP_1}.unity";
            case _LONG_MAP_1:
                return $"Assets/Scenes/Setup/{_LONG_MAP_1}.unity";
            default:
                return _MAIN_MENU;
        }
    }
}
