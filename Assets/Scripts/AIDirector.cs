using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIDirector : MonoBehaviour {

    public static AIDirector Instance;

    [SerializeField]
    List<Cover> m_coverList;

    void Awake()
    {
        Instance = this;
    }

    public Cover GetClosestCover(Vector3 _position, float _maxDistanceToCover)
    {
        float minDistance = _maxDistanceToCover;
        Cover closestCover = null;
        for (int i = 0; i < m_coverList.Count; i++)
        {
            
            Cover cover = m_coverList[i];
            if (cover.isOcupied)
                continue;

            float currentDistance = Vector3.Distance(_position, cover.coverTransform.position);
            if (currentDistance < minDistance)
            {
                minDistance = currentDistance;
                closestCover = cover;
            }
        }

        return closestCover;
    }
}
