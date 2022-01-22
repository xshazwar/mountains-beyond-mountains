using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditor.Callbacks;
using System.Collections.Generic;

#if __MICROSPLAT__
using JBooth.MicroSplat;
#endif

namespace xshazwar.terrain.microsplat.Editor {
#if __MICROSPLAT__
   [InitializeOnLoad]
   public class MicroSplatMBM : FeatureDescriptor
   {
      const string sDefine = "__MICROSPLAT_MBM__";
      static MicroSplatMBM()
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
         return "Mountains Beyond Mountains";
      }

      public enum DefineFeature
      {
         _MBM_HEIGHT,
         kNumFeatures,
      }

      static TextAsset properties;
      static TextAsset funcs;
      static TextAsset cbuffer;
      static TextAsset defines;

      public enum MBMMode
      {
         None = 0,
         Heights
      }

      public MBMMode mbmMode;
      public bool alphaBelowHeight;
      public int textureIndex;
      

      //   requires placement into core module @ microsplat_terrain_body.txt && microsplat_terrain_core_vertex.txt
      //   see /Fragments/ _func for details
 

      GUIContent CMBMMode = new GUIContent("Mountains Beyond Mountains", "Procedural Height Mapping");
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
         mbmMode = (MBMMode)EditorGUILayout.EnumPopup(CMBMMode, mbmMode);
      }

      public override void DrawShaderGUI(MicroSplatShaderGUI shaderGUI, MicroSplatKeywords keywords, Material mat, MaterialEditor materialEditor, MaterialProperty[] props)
      {
         if (mbmMode != MBMMode.None)
         {
            if (MicroSplatUtilities.DrawRollup("MountainsBeyondMountains"))
            {
               //TODO add some options to the shader generation
               if (mat.HasProperty("_MMData") && mbmMode != MBMMode.None)
               {}
            }
         }

      }

      public override string[] Pack()
      {
         List<string> features = new List<string>();
         if (mbmMode == MBMMode.Heights)
         {
            features.Add(GetFeatureName(DefineFeature._MBM_HEIGHT));
         }
         return features.ToArray();
      }

      public override void Unpack(string[] keywords)
      {
         mbmMode = MBMMode.None;
         if (HasFeature(keywords, DefineFeature._MBM_HEIGHT))
         {
            mbmMode = MBMMode.Heights;
         }
      }

      public override void InitCompiler(string[] paths)
      {
         for (int i = 0; i < paths.Length; ++i)
         {
            string p = paths[i];
            if (p.EndsWith("microsplat_properties_mbm.txt"))
            {
               properties = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            }
            if (p.EndsWith("microsplat_func_mbm.txt"))
            {
               funcs = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            }
            if (p.EndsWith ("microsplat_cbuffer_mbm.txt"))
            {
               cbuffer = AssetDatabase.LoadAssetAtPath<TextAsset> (p);
            }
            if (p.EndsWith ("microsplat_defines_mbm.txt"))
            {
               defines = AssetDatabase.LoadAssetAtPath<TextAsset> (p);
            }
         }
      }

      public override void WriteProperties(string[] features, System.Text.StringBuilder sb)
      {
         if (mbmMode != MBMMode.None)
         {
            sb.Append(properties.text);
         }
      }

      public override void WriteFunctions(string [] features, System.Text.StringBuilder sb)
      {
         if (mbmMode != MBMMode.None)
         {
            sb.Append(funcs.text);
         }
      }

      public override void WritePerMaterialCBuffer (string[] features, System.Text.StringBuilder sb)
      {
         if (mbmMode != MBMMode.None)
         {
            sb.Append(cbuffer.text);
         }
      }

      public override void WriteSharedFunctions(string[] features, System.Text.StringBuilder sb) {
         if (mbmMode != MBMMode.None)
         {
            sb.Append(defines.text);
         }
      }

      // TODO
      public override void ComputeSampleCounts(string[] features, ref int arraySampleCount, ref int textureSampleCount, ref int maxSamples, ref int tessellationSamples, ref int depTexReadLevel)
      {}

   }   

#endif
}