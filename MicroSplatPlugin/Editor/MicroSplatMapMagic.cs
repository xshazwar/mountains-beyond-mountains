using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Collections.Generic;

#if __MICROSPLAT__
using JBooth.MicroSplat;
#endif

namespace xshazwar.MM2 {
#if __MICROSPLAT__
   [InitializeOnLoad]
   public class MicroSplatMapMagic : FeatureDescriptor
   {
      const string sDefine = "__MICROSPLAT_MM2B__";
      static MicroSplatMapMagic()
      {
         MicroSplatDefines.InitDefine(sDefine);
      }
      [PostProcessSceneAttribute (0)]
      public static void OnPostprocessScene()
      { 
         MicroSplatDefines.InitDefine(sDefine);
      }

      public override string ModuleName()
      {
         return "MapMagic Billboard";
      }

      public enum DefineFeature
      {
         _MM2HEIGHT,
         kNumFeatures,
      }

      static TextAsset properties;
      static TextAsset funcs;
      static TextAsset cbuffer;
      static TextAsset defines;

      public enum MM2Mode
      {
         None = 0,
         Heights
      }

      public MM2Mode mm2Mode;
      public bool alphaBelowHeight;
      public int textureIndex;
      

    //   place into ,icrosplat_terrain_body.txt
    //   MicroSplatLayer SurfImpl
    // #if _MM2HEIGHT
    //     i.worldPos.y = MapMagicDisplacement(i.worldPos);
    //     i.worldHeight = i.worldPos.y;
    // #endif      

      GUIContent CMM2Mode = new GUIContent("MapMagic2", "Procedural Height Mapping");
      // Can we template these somehow?
      static Dictionary<DefineFeature, string> sFeatureNames = new Dictionary<DefineFeature, string>();
      public static string GetFeatureName(DefineFeature feature)
      {
         string ret;
         if (sFeatureNames.TryGetValue(feature, out ret))
         {
            return ret;
         }
         string fn = System.Enum.GetName(typeof(DefineFeature), feature);
         sFeatureNames[feature] = fn;
         return fn;
      }

      public static bool HasFeature(string[] keywords, DefineFeature feature)
      {
         string f = GetFeatureName(feature);
         for (int i = 0; i < keywords.Length; ++i)
         {
            if (keywords[i] == f)
               return true;
         }
         return false;
      }

      static GUIContent CHeight = new GUIContent("Texture Index", "Texture Index which is considered 'transparent'");

      public override string GetVersion()
      {
         return "3.9";
      }

      public override void DrawFeatureGUI(MicroSplatKeywords keywords)
      {
         mm2Mode = (MM2Mode)EditorGUILayout.EnumPopup(CMM2Mode, mm2Mode);
      }

      public override void DrawShaderGUI(MicroSplatShaderGUI shaderGUI, MicroSplatKeywords keywords, Material mat, MaterialEditor materialEditor, MaterialProperty[] props)
      {
         if (mm2Mode != MM2Mode.None)
         {
            if (MicroSplatUtilities.DrawRollup("MM2"))
            {
                  if (mat.HasProperty("_MMData") && mm2Mode != MM2Mode.None)
                  {
                    //  Vector4 vals = shaderGUI.FindProp("_AlphaData", props).vectorValue;
                    //  Vector4 newVals = vals;
                    //  if (alphaHole == AlphaHoleMode.SplatIndex)
                    //  {
                    //     newVals.x = (int)EditorGUILayout.IntSlider(CTextureIndex, (int)vals.x, 0, 16);
                    //  }
                    //  if (alphaBelowHeight)
                    //  {
                    //     newVals.y = EditorGUILayout.FloatField(CWaterLevel, vals.y);
                    //  }
                    //  if (newVals != vals)
                    //  {
                    //     shaderGUI.FindProp("_AlphaData", props).vectorValue = newVals;
                    //  }
                  
               }
            }
         }

      }

      public override string[] Pack()
      {
         List<string> features = new List<string>();
         if (mm2Mode == MM2Mode.Heights)
         {
            features.Add(GetFeatureName(DefineFeature._MM2HEIGHT));
         }
         return features.ToArray();
      }

      public override void Unpack(string[] keywords)
      {
         mm2Mode = MM2Mode.None;
         if (HasFeature(keywords, DefineFeature._MM2HEIGHT))
         {
            mm2Mode = MM2Mode.Heights;
         }
      }

      public override void InitCompiler(string[] paths)
      {
         for (int i = 0; i < paths.Length; ++i)
         {
            string p = paths[i];
            if (p.EndsWith("microsplat_properties_mapmagic.txt"))
            {
               properties = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            }
            if (p.EndsWith("microsplat_func_mapmagic.txt"))
            {
               funcs = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            }
            if (p.EndsWith ("microsplat_cbuffer_mapmagic.txt"))
            {
               cbuffer = AssetDatabase.LoadAssetAtPath<TextAsset> (p);
            }
            if (p.EndsWith ("microsplat_defines_mapmagic.txt"))
            {
               defines = AssetDatabase.LoadAssetAtPath<TextAsset> (p);
            }
         }
      }

      public override void WriteProperties(string[] features, System.Text.StringBuilder sb)
      {
         if (mm2Mode != MM2Mode.None)
         {
            sb.Append(properties.text);
            // if (alphaHole == AlphaHoleMode.ClipMap)
            // {
            //    sb.AppendLine("      _AlphaHoleTexture(\"ClipMap\", 2D) = \"white\" {}");
            // }
         }
      }

      public override void WriteFunctions(string [] features, System.Text.StringBuilder sb)
      {
         if (mm2Mode != MM2Mode.None)
         {
            sb.Append(funcs.text);
         }
      }

      public override void WritePerMaterialCBuffer (string[] features, System.Text.StringBuilder sb)
      {
         if (mm2Mode != MM2Mode.None)
         {
            sb.Append(cbuffer.text);
         }
      }

      public override void WriteSharedFunctions(string[] features, System.Text.StringBuilder sb) {
         if (mm2Mode != MM2Mode.None)
         {
            sb.Append(defines.text);
         }
      }

      public override void ComputeSampleCounts(string[] features, ref int arraySampleCount, ref int textureSampleCount, ref int maxSamples, ref int tessellationSamples, ref int depTexReadLevel)
      {
        //  if (alphaHole == AlphaHoleMode.ClipMap)
        //  {
        //     textureSampleCount++;
        //  }

      }

   }   

#endif
}