using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraScript : MonoBehaviour
{
    private Camera cam;
    [SerializeField] Transform player;
    [SerializeField] float cameraSmoothing;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        Vector2 position = cam.transform.position;
        Vector2 direction = player.position;
        
        RaycastHit2D hit = Physics2D.Linecast(position, direction);
        if (hit)
        {
            //Debug.DrawRay(position, direction, Color.red);
            Vector2 target = hit.point;
            
            Vector2 smoothedPosition = Vector2.Lerp(transform.position, target,  cameraSmoothing* Time.deltaTime);
            
            transform.position = new Vector3(smoothedPosition.x, smoothedPosition.y, -10);
            
        }
        
    }
}
