using Services.Audio;
using UnityEditor;
using UnityEngine;

public static class SoundPass2Temp
{
    public static string Run()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Contents/Audio/SFX", "Assets/Contents/Audio/Music" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("Axe_") && !path.Contains("Scream") && !path.Contains("Tension")) continue;
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            var s = imp.defaultSampleSettings;
            bool isMusic = path.Contains("Music");
            s.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.7f;
            imp.defaultSampleSettings = s; imp.forceToMono = !isMusic; imp.loadInBackground = isMusic;
            imp.SaveAndReimport();
        }
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<AudioConfig>("Assets/Contents/Data/AudioConfig.asset"));
        var arr = so.FindProperty("_sounds");
        void Add(SoundType t, float vol, Vector2 pitch, float blend, float dist, params string[] clips)
        {
            SerializedProperty e = null;
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).FindPropertyRelative("_type").intValue == (int)t) e = arr.GetArrayElementAtIndex(i);
            if (e == null) { arr.arraySize++; e = arr.GetArrayElementAtIndex(arr.arraySize - 1); e.FindPropertyRelative("_type").intValue = (int)t; }
            var c = e.FindPropertyRelative("_clips"); c.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) c.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Contents/Audio/" + clips[i]);
            e.FindPropertyRelative("_volume").floatValue = vol; e.FindPropertyRelative("_pitch").vector2Value = pitch;
            e.FindPropertyRelative("_spatialBlend").floatValue = blend; e.FindPropertyRelative("_maxDistance").floatValue = dist;
        }
        var v = new Vector2(0.93f, 1.07f);
        Add(SoundType.AxeSwing, 0.55f, v, 0f, 10f, "SFX/Axe_Swing_0.wav", "SFX/Axe_Swing_1.wav", "SFX/Axe_Swing_2.wav");
        Add(SoundType.AxeHitWood, 0.9f, v, 0.6f, 18f, "SFX/Axe_Hit_Wood_0.wav", "SFX/Axe_Hit_Wood_1.wav", "SFX/Axe_Hit_Wood_2.wav");
        Add(SoundType.AxeHitHard, 0.8f, v, 0.6f, 18f, "SFX/Axe_Hit_Hard_0.wav", "SFX/Axe_Hit_Hard_1.wav", "SFX/Axe_Hit_Hard_2.wav");
        Add(SoundType.Scream, 0.9f, Vector2.one, 0.8f, 30f, "SFX/Scream_Help.wav");
        so.ApplyModifiedPropertiesWithoutUndo();

        var root = PrefabUtility.LoadPrefabContents("Assets/Contents/Prefabs/Systems/AudioService.prefab");
        var svc = new SerializedObject(root.GetComponent<AudioService>());
        var music = (AudioSource)svc.FindProperty("_music").objectReferenceValue;
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Contents/Audio/Music/Music_Tension.ogg");
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Contents/Prefabs/Systems/AudioService.prefab");
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.MoveAssetToTrash("Assets/Contents/Audio/Music/Music_Calm.ogg");
        AssetDatabase.SaveAssets();
        return "ok " + music.clip;
    }
}
