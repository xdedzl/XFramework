using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using XFramework.Entity;
using static XFramework.Utility;

namespace XFramework.UI
{
    public class TipEntity : Entity.Entity
    {
        private TextMeshProUGUI tmp;

        public string text
        {
            get
            {
                return tmp.text;
            }
            set
            {
                tmp.text = value;
            }
        }

        public Color color
        {
            get
            {
                return tmp.color;
            }
            set
            {
                tmp.color = value;
            }
        }

        public override void OnInit()
        {
            tmp = GetComponent<TextMeshProUGUI>();
        }

        public override void OnAllocate(IEntityData entityData)
        {
            gameObject.SetActive(true);
            Timer.Register(0.5f, () =>
            {
                Recycle();
            });
        }

        public override void OnRecycle()
        {
            gameObject.SetActive(false);

        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            transform.Translate(new Vector3(0, 1, 0));
        }
    }
}

