using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimationTransitioner : MonoBehaviour
{
    private Animator anim;

    [SerializeField] public bool talk;
    [SerializeField] private bool walk;
    [SerializeField] public bool thinking;
    [SerializeField] private bool turnR;
    [SerializeField] private bool turnL;

    public static AnimationTransitioner instance;

    private void Awake()
    {
        instance = this;
        anim = GetComponent<Animator>();
    }

    private void Update()
    {
        anim.SetBool("isTalk", talk);
        anim.SetBool("isWalk", walk);
        anim.SetBool("isThinking", thinking);
        anim.SetBool("isTurnR", turnR);
        anim.SetBool("isTurnL", turnL);
    }

    public void SetTalking(bool value)
    {
        talk = value;
        anim.SetBool("isTalk", value);
    }

    public void SetWalking(bool value)
    {
        walk = value;
        anim.SetBool("isWalk", value);
    }
    
    public void SetThinking(bool value)
    {
        thinking = value;
        anim.SetBool("isThinking", value);
    }
    public void SetTurnR(bool value)
    {
        turnR = value;
        anim.SetBool("isTurnR", value);
    }
    public void SetTurnL(bool value)
    {
        turnL = value;
        anim.SetBool("isTurnL", value);
    }
}