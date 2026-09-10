using System;
using UnityEngine;

namespace TrueInputSystem.Readme
{
    /// <summary>
    /// Data container for the TrueInputSystem package Readme.
    /// The visual "TIS" hero banner and section layout are rendered by
    /// TISReadmeEditor (see Editor/TISReadmeEditor.cs) — this class only
    /// holds the content.
    /// </summary>
    [CreateAssetMenu(fileName = "TISReadme", menuName = "TrueInputSystem/Readme Asset", order = 1)]
    public class TISReadme : ScriptableObject
    {
        public Texture2D icon;
        public string title = "TrueInputSystem (TIS)";
        public Section[] sections;
        public bool loadedLayout;

        [Serializable]
        public class Section
        {
            public string heading;

            [TextArea(3, 20)]
            public string text;

            public string linkText;
            public string url;
        }
    }
}
