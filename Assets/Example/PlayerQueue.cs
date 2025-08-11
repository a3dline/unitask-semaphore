using System;
using System.Threading;
using AUniTaskSemaphore;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Example
{
    public class PlayerQueue : MonoBehaviour
    {
        [SerializeField]
        private int _playerCount;

        [SerializeField]
        private Vector3 _positionMultiplayer;

        [SerializeField]
        private Transform _target;

        [SerializeField]
        private GameObject _playerPrefab;
        
        private UniTaskSemaphore _semaphore = new(3);
        private CancellationTokenSource _cts = new();
        private void Start()
        {
            for (int i = 0; i < _playerCount; i++)
            {
                var position = _playerPrefab.transform.position + _positionMultiplayer * (i + 1);
                var playerInstance = Instantiate(_playerPrefab, position, Quaternion.identity);
                AddToQueue(playerInstance.transform, _cts.Token).Forget();
            }
            
            _playerPrefab.SetActive(false);
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid AddToQueue(Transform playerInstance, CancellationToken token)
        {
            using var _ = await _semaphore.Acquire(token);
            var speed = Random.Range(1f, 1.5f);
            await MoveUpToY(playerInstance, speed, token);            
        }

        public async UniTask MoveUpToY(Transform player, float speed, CancellationToken token)
        {
            var target = _target.transform.position.y;
            while (player.position.y < target)
            {
                var pos = player.position;
                pos.y = Mathf.MoveTowards(pos.y, target, speed * Time.deltaTime);
                player.position = pos;
                token.ThrowIfCancellationRequested();
                await UniTask.Yield();
            }
        }
    }
}