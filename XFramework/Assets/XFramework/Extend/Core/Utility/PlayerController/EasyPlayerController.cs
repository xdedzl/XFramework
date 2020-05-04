using UnityEngine;

public class EasyPlayerController : MonoBehaviour
{
    public float speed = 10;
    public float rotateSpeed = 100;

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        float deltaTime = Time.deltaTime;
        transform.Translate(0, 0, speed * vertical * deltaTime, Space.Self);
        transform.Rotate(0, rotateSpeed * horizontal * deltaTime, 0);
    }
}