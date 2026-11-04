using UnityEngine;

public class MrHeckShotBehaviour : StateMachineBehaviour
{
    // Called when the shot animation finishes
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // Destroy Mr. Heck’s GameObject when the shot animation ends
        GameObject heck = animator.gameObject;
        Object.Destroy(heck);

        // If you prefer to disable instead of destroy:
        // heck.SetActive(false);
    }
}
