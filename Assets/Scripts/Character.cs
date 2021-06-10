using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Character : MonoBehaviour {

    public enum States { idle,moving,hitStun,shocking,attacking, floating}
    public States state = States.idle;
    public bool dead = false;
    public bool hitStun = false;
    public AudioSource audioSource;
    public bool friend = false;
    public Transform target;
    public ParticleSystem damageParticle;
	
    public float maxDrainEnergy = 10;
    public float energyLeft = 10;

    public bool visible = true;
    public ShootData shootData;
    public Action OnReceiveDamage;    

    // Use this for initialization
	void Start () {
        audioSource = GetComponent<AudioSource>();
	}

	// Update is called once per frame
	void Update () {
		
	}

    public void SetState(States newState)
    {
        state = newState;
    }

    public void EndKill()
    {

    }

    public void Fire()
    {
        //Debug.Log("Fire");

        GameObject fireEffect = Instantiate(shootData.BulletEffect);
        fireEffect.transform.position= transform.position + 0.3f * Vector3.up+0.2f*transform.forward;
        fireEffect.transform.forward = transform.forward;

        DamageArea thisBullet= Instantiate(shootData.Bullet);
        thisBullet.transform.position = transform.position + 0.3f * Vector3.up;
        thisBullet.transform.forward = transform.forward;
        thisBullet.friend = friend;
        thisBullet.speed = shootData.BulletSpeed;
    }

    public virtual void DealDamage(float val)
    {
	    audioSource.PlayOneShot(AudioManager.getInstance().commonHit);
	    damageParticle.Play();
	    OnReceiveDamage?.Invoke();
    }
    
    protected void DealDamage(float val, AudioClip audioClip)
    {
	    audioSource.PlayOneShot(audioClip);
	    damageParticle.Play();
	    OnReceiveDamage?.Invoke();
    }
}
