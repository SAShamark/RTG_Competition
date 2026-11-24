using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class EntryPoint : MonoBehaviour
{
    [SerializeField] private GameManager _gameManager = new();

    private void Start()
    {
        _gameManager.Initialize();
    }

    private void Update()
    {
        _gameManager.Tick(Time.deltaTime);
    }

    private void LateUpdate()
    {
        _gameManager.LateTick();
    }

    private void OnDestroy()
    {
        _gameManager.Dispose();
    }
}
