using UnityEngine;
namespace DuckGame.Maps.V10 {
[DefaultExecutionOrder(-200)]public sealed class ForestImpactStartup:MonoBehaviour {
 public ForestImpact effect;void Awake(){if(effect==null||effect.ring==null||effect.streak==null){Debug.LogError("V10 impact material reference missing",this);enabled=false;return;}ForestImpactPool.Warmup(effect);}
}
}
