using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Models
{
    using UnityEngine;
    using UnityEngine.UI;

    public class LipSyncController : MonoBehaviour
    {
        // Assign the mesh that has blend shapes (e.g., lower_teeth_mesh)
        public static SkinnedMeshRenderer mouthMesh;
        //private static LipSyncController lipController;
        //public static LipSyncController LipController
        //{
        //    get
        //    {
        //        if (lipController == null)
        //        {
        //            lipController = new LipSyncController();
        //        }
        //        return lipController;
        //    }
        //}
        // BlendShape indices for specific visemes (these need to be checked in the Inspector)
        private static int blendShapeAh;
        private static int blendShapeEe;
        private static int blendShapeOo;

        public Button testLip;
        void Start()
        {
            // Assuming mouthMesh is assigned via the Inspector
            if (mouthMesh != null)
            {
                // Assign the blend shape indices (you need to find these from the Inspector)
                blendShapeAh = mouthMesh.sharedMesh.GetBlendShapeIndex("Ah"); // Example for "Ah" viseme
                blendShapeEe = mouthMesh.sharedMesh.GetBlendShapeIndex("Ee"); // Example for "Ee" viseme
                blendShapeOo = mouthMesh.sharedMesh.GetBlendShapeIndex("Oo"); // Example for "Oo" viseme
            }
            testLip.onClick.AddListener(()=>UpdateLipSync("Ah"));
        }

        // Method to animate the mouth using blend shapes based on viseme values
        public static void UpdateLipSync(string viseme)
        {
            if (mouthMesh == null) return;

            // Reset all blend shapes before applying new one
            mouthMesh.SetBlendShapeWeight(blendShapeAh, 0);
            mouthMesh.SetBlendShapeWeight(blendShapeEe, 0);
            mouthMesh.SetBlendShapeWeight(blendShapeOo, 0);

            // Apply the appropriate blend shape weight based on the viseme
            switch (viseme)
            {
                case "Ah":
                    mouthMesh.SetBlendShapeWeight(blendShapeAh, 100); // Set 100% influence for "Ah"
                    break;
                case "Ee":
                    mouthMesh.SetBlendShapeWeight(blendShapeEe, 100); // Set 100% influence for "Ee"
                    break;
                case "Oo":
                    mouthMesh.SetBlendShapeWeight(blendShapeOo, 100); // Set 100% influence for "Oo"
                    break;
                    // Add more cases for other visemes as needed
            }
        }
    }
}
