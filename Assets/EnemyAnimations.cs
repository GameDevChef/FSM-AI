using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimations : MonoBehaviour {

    const string HORIZONTAL = "horizontal";
    const string VERTICAL = "vertical";
    const string ALERT = "alert";

    Animator m_anim;

    private void Awake()
    {
        m_anim = GetComponentInChildren<Animator>();
    }

    public void AnimatiansNormal(Vector3 _velocity, bool _isAlert)
    {
        Debug.Log("normal " + _isAlert);
        Vector3 relativeVelocity = transform.InverseTransformDirection(_velocity);
        
        //Debug.Log(relativeVelocity.z);
        float z = relativeVelocity.z;
        if (!_isAlert)
        {
            z = Mathf.Clamp(relativeVelocity.z, 0f, .5f);
        }
        
        m_anim.SetFloat(VERTICAL, z, 0.1f, Time.deltaTime);
    }

    public void AnimatiansAlert(Vector3 _velocity)
    {
        Debug.Log("alert");
        Vector3 relativeVelocity = transform.InverseTransformDirection(_velocity);
        relativeVelocity.Normalize();

        float x = relativeVelocity.x;
        float z = relativeVelocity.z;

        m_anim.SetFloat(VERTICAL, z, 0.1f, Time.deltaTime);
        m_anim.SetFloat(HORIZONTAL, x, 0.1f, Time.deltaTime);
    }

    public void SetAlertBool(bool _value)
    {
        m_anim.SetBool(ALERT, _value);
    }
}
