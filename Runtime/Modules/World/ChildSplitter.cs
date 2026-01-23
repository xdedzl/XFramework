using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class ChildSplitter : WorldSplitter
{
    public override IReadOnlyList<GameObject> GetGameObjects()
    {
        var children = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
            children.Add(transform.GetChild(i).gameObject);
        return children;
    }
}
