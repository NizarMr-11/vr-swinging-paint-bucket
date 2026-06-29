using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

public class PixelSpriteCreator : MonoBehaviour
{
    [MenuItem("Assets/Create/Pixel Sprite")]
    public static void CreatePixelSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        
        string path = "Assets/Resources/PixelSprite.asset";
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32);
        
        AssetDatabase.CreateAsset(sprite, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("Pixel sprite created at: " + path);
    }
}
#endif