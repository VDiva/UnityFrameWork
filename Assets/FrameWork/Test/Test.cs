using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace FrameWork.Test
{
    public class Test : MonoBehaviour
    {
        [Button(ButtonSizes.Large)]
        public void SayHello()
        {
            Debug.Log("hello");
        }

        [FilePath(Extensions = ".unity")]
        public string scene;
        
        [PreviewField,Required,AssetsOnly]
        public GameObject prefab;

        [HorizontalGroup("Split",Width = 100),HideLabel,PreviewField(100)]
        public Texture2D icon;

        
        [VerticalGroup("Split/Properties")]
        public string minionName;
        [VerticalGroup("Split/Properties")]
        public string health;
        [VerticalGroup("Split/Properties")]
        public string damage;

        [LabelText("$IAmLable")]
        public string IAmLable;

        [ListDrawerSettings(
            CustomAddFunction = "CreateNewGUID",
            CustomRemoveIndexFunction = "RemoveGUID"
            )]
        public List<string> GuidList;


        [TabGroup("FirstTab")]
        public int FirstTab;
        [TabGroup("FirstTab")]
        public int ScondTab;

        [TabGroup("SecondTab")]
        public float FloatValue;


        [TabGroup("SecondTab"),Button]
        public void Button()
        {
            Debug.Log("Btn");
        }
        
        
        [Button(ButtonSizes.Large)]
        [FoldoutGroup("Buttons in Boxes")]
        [HorizontalGroup("Buttons in Boxes/Horizontal", Width = 60)]
        [BoxGroup("Buttons in Boxes/Horizontal/One")]
        public void Button1() { }

        [Button(ButtonSizes.Large)]
        [BoxGroup("Buttons in Boxes/Horizontal/Two")]
        public void Button2() { }

        [Button]
        [BoxGroup("Buttons in Boxes/Horizontal/Double")]
        public void Accept() { }

        [Button]
        [BoxGroup("Buttons in Boxes/Horizontal/Double")]
        public void Cancel() { }
        
        
        
        private string CreateNewGUID()
        {
            return Guid.NewGuid().ToString();
        }

        private void RemoveGUID(int index)
        {
            this.GuidList.RemoveAt(index);
        }
        
        
        [ValidateInput("IsValid")]
        public int GreaterThanZero;

        private bool IsValid(int value)
        {
            return value > 0;
        }

        [OnValueChanged("UpdateRigidbodyReference")]
        public GameObject Prefab;

        private Rigidbody prefabRigidbody;

        private void UpdateRigidbodyReference()
        {
            if (this.Prefab != null)
            {
                this.prefabRigidbody = this.Prefab.GetComponent<Rigidbody>();
            }
            else
            {
                this.prefabRigidbody = null;
            }
        }
        
        
        [Required]
        public GameObject RequiredReference;

        [InfoBox("This message is only shown when MyInt is even", "IsEven")]
        public int MyInt;

        private bool IsEven()
        {
            return this.MyInt % 2 == 0;
        }
        
        
        [PropertyOrder(1)]
        public int Last;

        [PropertyOrder(-1)]
        public int First;

// All properties default to 0.
        public int Middle;
    }
}