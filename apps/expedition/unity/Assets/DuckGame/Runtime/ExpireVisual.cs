using UnityEngine;
namespace DuckGame {
public sealed class ExpireVisual:MonoBehaviour {
 float remaining,total;Material owned;
 public void Initialize(float time,Material material=null){remaining=total=time;owned=material;}
 void Update(){remaining-=Time.deltaTime;transform.localScale+=Vector3.one*Time.deltaTime*.45f;if(remaining<=0)Destroy(gameObject);}
 void OnDestroy(){if(owned!=null)Destroy(owned);}
}
}
