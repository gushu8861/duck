using UnityEngine;
namespace DuckGame {
public enum OrbSpawnStrategy { HoveringRound, HatTip }
[CreateAssetMenu(menuName="Duck Game/Orb Configuration")]
public sealed class OrbConfig:ScriptableObject {
 [Min(.05f)] public float loadInterval=.8f;
 [HideInInspector, Tooltip("Legacy serialized compatibility only. Loading now controls the whole firing cycle.")]
 public float shotInterval=.8f;
 [Header("Hit feedback")]
 public Color enemyImpactColor=new Color(1f,.78f,.32f);
 [Range(.5f,2)] public float impactScale=1;
 [Min(1)] public float speed=32;
 [Range(1,8)] public int projectileCount=1;
 [Range(0,12)] public float spreadDegrees=3;
 [Min(.01f)] public float radius=.10f;
 [Min(.1f)] public float lifetime=4;
 public OrbSpawnStrategy spawnStrategy=OrbSpawnStrategy.HoveringRound;
 public Vector3 spawnOffset=Vector3.zero;
 public Color orbColor=new Color(.22f,.85f,1);
 public OrbEffect impactEffect;
 public bool replaceDefaultImpact;
}
public readonly struct OrbShotContext {
 public readonly Vector3 Origin,Direction;public readonly int Index;public readonly GameObject Owner;
 public OrbShotContext(Vector3 origin,Vector3 direction,int index,GameObject owner){Origin=origin;Direction=direction;Index=index;Owner=owner;}
}
public readonly struct OrbImpactContext {
 public readonly Collider Collider;public readonly Vector3 Point,Normal;public readonly bool FromSweep;
 public OrbImpactContext(Collider collider,Vector3 point,Vector3 normal,bool fromSweep){Collider=collider;Point=point;Normal=normal;FromSweep=fromSweep;}
}
public interface IOrbHitReceiver { void ReceiveOrbHit(OrbShotContext shot, OrbImpactContext impact); }
public abstract class OrbEffect:ScriptableObject {public abstract void OnImpact(OrbShotContext shot,OrbImpactContext impact);}
}
