using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerShootData", menuName = "ScriptableObjects/PlayerShootData", order = 1)]
public class PlayerShootData : ShootData
{
    public float BulletEnergyCost => _bulletEnergyCost;
    public String BtnName => _btnName;    

    [SerializeField][Range(0,100)] private float _bulletEnergyCost;
    [SerializeField] private String _btnName;
}
