using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using THSDK;


public class moving_script : MonoBehaviour
{
    public HolographicDevice device;
    public float speed = 5f;
    public Rigidbody myRigidbody;

    [SerializeField] private PlayButton playButton;
    [SerializeField] private AudioClip playGameSound;
    [SerializeField] private TriviaManager triviaManager;
    [SerializeField] private Sprite play;

   

    private void Start()
    {
        myRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void FixedUpdate()
    {
        Vector2 input = device.GetUser(0).GetController(0).GetJoystick(0);

        if (device.GetUser(0).GetController(0).GetButton(Button.Trigger))
        {
            Vector3 currentRotation = transform.localEulerAngles;
            if (currentRotation.y > 180) currentRotation.y -= 360;
            if (currentRotation.x > 180) currentRotation.x -= 360;

            // Calcular la nueva rotación
            float newRotationX = currentRotation.x - input.y * speed;
            float newRotationY = currentRotation.y + input.x * speed;

            // Limitar la rotación vertical (eje X)
            newRotationX = Mathf.Clamp(newRotationX, -45f, 45f);

            // Aplicar la nueva rotación
            Quaternion targetRotation = Quaternion.Euler(newRotationX, newRotationY, currentRotation.z);
            myRigidbody.MoveRotation(targetRotation);

            myRigidbody.velocity = Vector3.zero;
        }
        else
        {
            Vector3 movement = new Vector3(input.x, 0, input.y);
            movement = transform.TransformDirection(movement);
            movement.y = 0;
            movement *= speed * Time.fixedDeltaTime;

            myRigidbody.MovePosition(myRigidbody.position + movement);
        }
    }



    private void OnTriggerEnter(Collider other)
    {
        int questionLevel = 0;

        switch (other.tag)
        {
            case "Las Meninas":
                questionLevel = triviaManager.meninasDificulty;
                break;
            case "Composición Ocho de Kandinsky":
                questionLevel = triviaManager.kandinskyDificulty;
                break;
            case "Balloon Dog de Jeff Koons":
                questionLevel = triviaManager.balloonDificulty;
                break;
            case "Dali":
                questionLevel = triviaManager.daliDificulty;
                break;
            case "Keith Haring":
                questionLevel = triviaManager.keithDificulty;
                break;
        }

        if (questionLevel >= triviaManager.maxQuestions)
        {
            return; // No mostrar botón si no quedan preguntas
        }
        else
        {
            playButton.ShowButton();
            playButton.SetRoomName(other.tag);

            // Según si estamos jugando o no, poner la imagen correcta
            if (playButton.playing)
            {
                playButton.SetToStop();
            }
            else
            {
                playButton.SetToPlay();
            }

            SoundManager.instance.MakeSound(playGameSound, 0.5f);
        }        
    }

    private void OnTriggerExit(Collider other)
    {
        triviaManager.hideRanking();
        playButton.SetToPlay(); // Asegura que el botón vuelve a "Play" tras terminar
        playButton.HideButton();
    }
}

