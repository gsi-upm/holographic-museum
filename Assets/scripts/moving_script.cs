using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using THSDK;


public class moving_script : MonoBehaviour
{
    public HolographicDevice device;
    public float speed = 5f;
    public float rotationSpeed = 100f;
    public Rigidbody myRigidbody;

    public string exhibitionName; // Variable para almacenar el nombre de la exposición actual

    [SerializeField] private PlayButton playButton;
    [SerializeField] private AudioClip playGameSound;
    [SerializeField] private TriviaManager triviaManager;
    [SerializeField] private Sprite play;
    [SerializeField] public GameObject audioGuide;
    [SerializeField] public GameObject stopAudioGuide;
    public static moving_script instance;

    private void Awake()
    {
        instance = this;
        audioGuide.SetActive(false);
        stopAudioGuide.SetActive(false);
    }


    private void Start()
    {
        myRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
    }

    [Header("Teclas (configurables en Inspector)")]
    [SerializeField] private KeyCode forwardKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode backKey = KeyCode.DownArrow;
    [SerializeField] private KeyCode rightKey = KeyCode.RightArrow;
    [SerializeField] private KeyCode leftKey = KeyCode.LeftArrow;

    private void FixedUpdate()
    {
        var user = device.GetUser(0);
        var controller = user.GetController(0);

        Vector2 joystickInput = controller.GetJoystick(0);
        bool triggerPressed = controller.GetButton(Button.Trigger) || Input.GetKey(KeyCode.Space); // Permite usar la barra espaciadora como disparador

        Vector2 keyboardInput = Vector2.zero;
        if (Input.GetKey(forwardKey)) keyboardInput.y += 1f;
        if (Input.GetKey(backKey))    keyboardInput.y -= 1f;
        if (Input.GetKey(rightKey))   keyboardInput.x += 1f;
        if (Input.GetKey(leftKey))    keyboardInput.x -= 1f;

        Vector2 input = Vector2.ClampMagnitude(joystickInput + keyboardInput, 1f);

        if (triggerPressed)
        {
            Vector3 currentRotation = myRigidbody.rotation.eulerAngles;

            if (currentRotation.y > 180f) currentRotation.y -= 360f;
            if (currentRotation.x > 180f) currentRotation.x -= 360f;

            float newRotationX = currentRotation.x - input.y * speed;
            float newRotationY = currentRotation.y + input.x * speed;
            newRotationX = Mathf.Clamp(newRotationX, -45f, 45f);

            Quaternion targetRotation = Quaternion.Euler(newRotationX, newRotationY, 0f);
            myRigidbody.MoveRotation(targetRotation);

            myRigidbody.linearVelocity = Vector3.zero;
            myRigidbody.angularVelocity = Vector3.zero;
        }
        else
        {
            Quaternion yawRotation = Quaternion.Euler(0f, myRigidbody.rotation.eulerAngles.y, 0f);

            Vector3 forward = yawRotation * Vector3.forward;
            Vector3 right = yawRotation * Vector3.right;

            Vector3 moveDirection = forward * input.y + right * input.x;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                moveDirection.Normalize();

                Vector3 desiredVelocity = moveDirection * speed;
                myRigidbody.linearVelocity = new Vector3(
                    desiredVelocity.x,
                    myRigidbody.linearVelocity.y,
                    desiredVelocity.z
                );
            }
            else
            {
                myRigidbody.linearVelocity = new Vector3(0f, myRigidbody.linearVelocity.y, 0f);
            }
            myRigidbody.angularVelocity = Vector3.zero;
        }
    }



    private void OnTriggerEnter(Collider other)
    {
        SoundManager.instance.ClearAgentClip();
        MusaFollower.instance?.OnRoomEntered(other.tag);
        exhibitionName = other.tag;
        int questionLevel = 0;

        questionLevel = triviaManager.artworkDifficulties[other.tag];

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
            if (questionLevel == 0)
            {
                // stopAudioGuide.SetActive(true);
                SoundManager.instance.PlayAudioguide(other.tag);
            }
            else
            {
                audioGuide.SetActive(true);
            }
        }        
    }

    private void OnTriggerExit(Collider other)
    {
        MusaFollower.instance?.OnRoomExited();
        triviaManager.hideRanking();
        triviaManager.hideResultsPanel();
        playButton.SetToPlay(); // Asegura que el botón vuelve a "Play" tras terminar
        playButton.HideButton();
        audioGuide.SetActive(false);
        exhibitionName = ""; // Limpiar el nombre de la exposición al salir
    }
}

