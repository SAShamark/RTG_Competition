using System;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace UI.Popups.Variants.MainMenu.Widgets
{
    [Serializable]
    public sealed class SparkBurst
    {
        private const float UpAngle = 90f;

        [SerializeField] private Image[] _sparks;
        [SerializeField] private Gradient _colorOverLife = new();
        [SerializeField] private Vector2 _speed = new(260f, 620f);
        [SerializeField] private Vector2 _lifetime = new(0.45f, 1.0f);
        [SerializeField] private Vector2 _size = new(8f, 20f);
        [SerializeField] private float _gravity = 520f;
        [SerializeField, Min(0f)] private float _drag = 1.6f;

        private Vector2[] _velocity;
        private float[] _age;
        private float[] _life;
        private float[] _startSize;
        private int _next;

        public int Capacity => _sparks.Length;

        public void Init()
        {
            _velocity = new Vector2[_sparks.Length];
            _age = new float[_sparks.Length];
            _life = new float[_sparks.Length];
            _startSize = new float[_sparks.Length];
            foreach (Image spark in _sparks)
            {
                spark.enabled = false;
            }
        }

        public void Emit(Vector2 position, int count, float spreadDegrees, float power)
        {
            for (int i = 0; i < count; i++)
            {
                int index = _next;
                _next = (_next + 1) % _sparks.Length;

                float angle = (UpAngle + Random.Range(-0.5f, 0.5f) * spreadDegrees) * Mathf.Deg2Rad;
                _velocity[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(_speed.x, _speed.y) * power;
                _age[index] = 0f;
                _life[index] = Random.Range(_lifetime.x, _lifetime.y);
                _startSize[index] = Random.Range(_size.x, _size.y) * power;

                Image spark = _sparks[index];
                spark.rectTransform.anchoredPosition = position;
                spark.enabled = true;
            }
        }

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _sparks.Length; i++)
            {
                Image spark = _sparks[i];
                if (!spark.enabled)
                {
                    continue;
                }

                _age[i] += deltaTime;
                float life = _age[i] / _life[i];
                if (life >= 1f)
                {
                    spark.enabled = false;
                    continue;
                }

                _velocity[i] *= Mathf.Exp(-_drag * deltaTime);
                _velocity[i].y -= _gravity * deltaTime;
                spark.rectTransform.anchoredPosition += _velocity[i] * deltaTime;
                spark.rectTransform.sizeDelta = Vector2.one * (_startSize[i] * (1f - life * life));
                spark.color = _colorOverLife.Evaluate(life);
            }
        }
    }
}
