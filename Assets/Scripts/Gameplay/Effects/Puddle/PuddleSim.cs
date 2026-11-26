using System;
using UnityEngine;

namespace Gameplay.Effects.Puddle
{
    [Serializable]
    public sealed class PuddleSim
    {
        public const int Channels = 4;

        private const float LitreVolume = 0.001f;
        private const float MaxSpreadShare = 0.2f;
        private const float NeighbourShare = 0.25f;
        private const int MaxStepsPerFrame = 3;
        private const float ReliefSeed = 37.3f;

        [SerializeField, Min(0.02f)] private float _cellSize = 0.1f;
        [SerializeField, Min(0.005f)] private float _stepTime = 0.033f;

        [Header("Pouring")]
        [SerializeField, Min(0.05f)] private float _splashRadius = 0.35f;
        [SerializeField, Min(0.05f)] private float _stirRadius = 1.1f;

        [Header("Spreading")]
        [SerializeField] private float _spreadRate = 6f;
        [SerializeField, Min(0.0001f)] private float _holdDepth = 0.0015f;
        [SerializeField, Min(0.0001f)] private float _fullDepth = 0.006f;

        [Header("Ground relief")]
        [SerializeField] private float _reliefHeight = 0.004f;
        [SerializeField, Min(0.05f)] private float _reliefSize = 1.2f;

        [Header("Drying")]
        [SerializeField] private float _dryRate = 0.00002f;
        [SerializeField] private float _stainFade = 0.01f;
        [SerializeField] private float _calmRate = 1.5f;

        private float[] _ground;
        private float[] _depth;
        private float[] _next;
        private float[] _stain;
        private float[] _stir;
        private float _debt;

        public int Columns { get; private set; }
        public int Rows { get; private set; }
        public float[] Pixels { get; private set; }
        public bool IsWet { get; private set; }

        public void Init(float width, float length)
        {
            Columns = Mathf.CeilToInt(width / _cellSize);
            Rows = Mathf.CeilToInt(length / _cellSize);
            int count = Columns * Rows;
            _ground = new float[count];
            _depth = new float[count];
            _next = new float[count];
            _stain = new float[count];
            _stir = new float[count];
            Pixels = new float[count * Channels];
            float frequency = _cellSize / _reliefSize;
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    _ground[y * Columns + x] = Mathf.PerlinNoise(x * frequency + ReliefSeed, y * frequency + ReliefSeed) * _reliefHeight;
                }
            }
        }

        public void Pour(float u, float v, float litres)
        {
            float centreX = u * Columns - 0.5f;
            float centreY = v * Rows - 0.5f;
            float splash = _splashRadius / _cellSize;
            float stir = _stirRadius / _cellSize;
            int reach = Mathf.CeilToInt(Mathf.Max(splash, stir));
            // A paraboloid mound of volume V over a disc of radius R peaks at 2V / (πR²).
            float peak = 2f * litres * LitreVolume / (Mathf.PI * _splashRadius * _splashRadius);
            int fromX = Mathf.Max(0, Mathf.FloorToInt(centreX) - reach);
            int toX = Mathf.Min(Columns - 1, Mathf.CeilToInt(centreX) + reach);
            int fromY = Mathf.Max(0, Mathf.FloorToInt(centreY) - reach);
            int toY = Mathf.Min(Rows - 1, Mathf.CeilToInt(centreY) + reach);
            for (int y = fromY; y <= toY; y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    float sqr = (x - centreX) * (x - centreX) + (y - centreY) * (y - centreY);
                    int cell = y * Columns + x;
                    _depth[cell] += peak * Mathf.Max(0f, 1f - sqr / (splash * splash));
                    _stir[cell] = Mathf.Max(_stir[cell], 1f - sqr / (stir * stir));
                }
            }

            IsWet = true;
        }

        public bool Tick(float deltaTime)
        {
            if (!IsWet)
            {
                return false;
            }

            _debt = Mathf.Min(_debt + deltaTime, _stepTime * MaxStepsPerFrame);
            bool hasStepped = false;
            while (_debt >= _stepTime)
            {
                _debt -= _stepTime;
                Spread();
                Settle();
                hasStepped = true;
            }

            return hasStepped;
        }

        // Water only runs downhill by surface level, and a film thinner than the hold depth clings to the ground and stays put.
        private void Spread()
        {
            float share = Mathf.Min(_spreadRate * _stepTime, MaxSpreadShare);
            Array.Copy(_depth, _next, _depth.Length);
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    int cell = y * Columns + x;
                    float limit = (_depth[cell] - _holdDepth) * NeighbourShare;
                    if (limit <= 0f)
                    {
                        continue;
                    }

                    if (x > 0)
                    {
                        Flow(cell, cell - 1, share, limit);
                    }

                    if (x < Columns - 1)
                    {
                        Flow(cell, cell + 1, share, limit);
                    }

                    if (y > 0)
                    {
                        Flow(cell, cell - Columns, share, limit);
                    }

                    if (y < Rows - 1)
                    {
                        Flow(cell, cell + Columns, share, limit);
                    }
                }
            }

            (_depth, _next) = (_next, _depth);
        }

        private void Flow(int from, int to, float share, float limit)
        {
            float drop = _ground[from] + _depth[from] - _ground[to] - _depth[to];
            if (drop <= 0f)
            {
                return;
            }

            float amount = Mathf.Min(drop * share, limit);
            _next[from] -= amount;
            _next[to] += amount;
        }

        private void Settle()
        {
            float dried = _dryRate * _stepTime;
            float faded = _stainFade * _stepTime;
            float calm = Mathf.Max(0f, 1f - _calmRate * _stepTime);
            bool isWet = false;
            for (int cell = 0; cell < _depth.Length; cell++)
            {
                float depth = Mathf.Max(0f, _depth[cell] - dried);
                float stain = Mathf.Max(_stain[cell] - faded, Mathf.Clamp01(depth / _holdDepth));
                float stir = Mathf.Max(0f, _stir[cell]) * calm;
                _depth[cell] = depth;
                _stain[cell] = stain;
                _stir[cell] = stir;
                int pixel = cell * Channels;
                Pixels[pixel] = depth / _fullDepth;
                Pixels[pixel + 1] = stir;
                Pixels[pixel + 2] = stain;
                isWet |= stain > 0f;
            }

            IsWet = isWet;
        }
    }
}
