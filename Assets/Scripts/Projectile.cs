using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 8f;
    public int damage = 1;
    public HitChannel channel;
    public int burnDamage;
    public float burnDuration;
    public float slowScale = 1f;
    public float slowDuration;
    public Transform target;
    public int life;
    public GameObject hitPS;

    public AudioClip hitSFX;

    bool gliding;
    Vector3 glidePoint;

    void OnEnable()
    {
        gliding = false;
    }

    void Update()
    {
        if (WaveManager.FrameOwned)
            return;
        Advance(Time.deltaTime);
    }

    public void Advance(float dt)
    {
        if (!gliding)
        {
            Enemy aimed = target != null ? target.GetComponent<Enemy>() : null;
            if (!SameLife(aimed))
            {
                glidePoint = target != null ? target.position : transform.position;
                gliding = true;
            }
        }

        Vector3 dest = gliding ? glidePoint : target.position;
        Vector3 from = transform.position;
        Vector3 offset = dest - from;
        float dist = offset.magnitude;
        if (dist > 0.0001f && dt > 0f)
        {
            transform.position = from + offset * (speed * dt / dist);
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90);
        }

        // A fast frame steps farther than the hit radius, so test the whole segment.
        Vector2 a = from;
        Vector2 b = transform.position;
        Vector2 c = dest;
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        float along = lenSq > 0.0000001f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / lenSq) : 0f;
        Vector2 nearest = a + ab * along;
        if ((nearest - c).sqrMagnitude > 0.15f * 0.15f)
            return;

        transform.position = new Vector3(nearest.x, nearest.y, Mathf.Lerp(from.z, transform.position.z, along));
        try
        {
            if (!gliding)
            {
                Enemy e = target != null ? target.GetComponent<Enemy>() : null;
                if (SameLife(e))
                {
                    e.Hurt(damage, channel);
                    if (e.health > 0)
                        e.HitStatus(burnDamage, burnDuration, slowScale, slowDuration, channel);
                }
            }
            if (hitPS != null)
                Instantiate(hitPS, transform.position, Quaternion.identity);
            if (hitSFX != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(hitSFX);
        }
        finally
        {
            ObjectPool.Release(gameObject);
        }
    }

    bool SameLife(Enemy e)
    {
        return e != null && e.gameObject.activeInHierarchy && e.life == life;
    }
}
