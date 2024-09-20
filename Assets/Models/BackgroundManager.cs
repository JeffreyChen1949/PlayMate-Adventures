using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Models
{
    using UnityEngine;
    using UnityEngine.UI;

    public class BackgroundManager : MonoBehaviour
    {
        public GameObject vikingRoom; // Reference to the "viking-room" model
        public GameObject mouseHome;
        public GameObject kidsRoom;
        public static Dictionary<string, GameObject> Backgrounds = new Dictionary<string, GameObject>();
        //public Button SwitchScene;

        private void Start()
        {
            //SwitchScene.onClick.AddListener(() => SwitchBackground("kidsRoom"));
            Backgrounds = new Dictionary<string, GameObject> { { "vikingRoom", vikingRoom }, { "mouseHome", mouseHome }, { "kidsRoom", kidsRoom } };
        }

        public static void SwitchBackground(string gameName)
        {
            foreach (string key in Backgrounds.Keys)
            {
                if (key.Equals(gameName))
                {
                    Backgrounds[key].SetActive(true);
                }
                else
                {
                    Backgrounds[key].SetActive(false);
                }
            }
        }
    }

}
