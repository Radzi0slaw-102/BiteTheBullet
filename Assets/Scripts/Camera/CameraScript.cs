using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraScript : MonoBehaviour
{
    private Camera cam;
    [SerializeField] Transform player;
    [SerializeField] float cameraSmoothing;
    [SerializeField] Vector2 offset = new Vector2(0f, 0.5f);
    void Start()
    {
        cam = this.GetComponent<Camera>();
    }
    
    void LateUpdate()
    {
        CameraFollow();
    }

    private void CameraFollow()
    {
        Vector3 target = new Vector3(player.position.x + offset.x, player.position.y + offset.y, transform.position.z);
        float t = 1f - Mathf.Exp(-cameraSmoothing * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target, t);
        
    }
}
