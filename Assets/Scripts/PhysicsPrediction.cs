using UnityEngine;
using UnityEngine.SceneManagement;

public class PhysicsPrediction : MonoBehaviour
{
    void CreatePhysicsScene()
    {
        var simulationScene = SceneManager.CreateScene("Simulation", new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        var physicsScene = simulationScene.GetPhysicsScene2D();


    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
