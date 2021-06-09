using UnityEngine;

[CreateAssetMenu(fileName = "ShootData", menuName = "ScriptableObjects/ShootData", order = 1)]
public class ShootData : ScriptableObject
{
    public DamageArea Bullet => _bullet;
    public GameObject BulletEffect => _bulletEffect;
    public float BulletSpeed => _bulletSpeed;
    
    [SerializeField] private DamageArea _bullet;
    [SerializeField] private GameObject _bulletEffect;
    [SerializeField] private float _bulletSpeed;
}
