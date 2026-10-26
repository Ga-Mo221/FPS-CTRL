// [Aemy 2026-10-26] FILE MOI.
using System;
using System.Collections;
using UnityEngine;

namespace Game.Boot
{
    /// <summary>
    /// Bien dich truoc cac bien the shader trong ShaderVariantCollection -> lan dau vao tran do giat.
    /// De trong collection = bo qua. (Tao collection: Project Settings > Graphics > Save to asset sau khi choi thu.)
    /// </summary>
    public sealed class ShaderWarmupStep : IBootStep
    {
        private readonly ShaderVariantCollection _collection;

        public ShaderWarmupStep(ShaderVariantCollection collection) { _collection = collection; }

        public string Label => "Shader warmup";
        public float Weight => _collection != null ? 1f : 0f;

        public IEnumerator Run(Action<float> report)
        {
            if (_collection != null && !_collection.isWarmedUp)
            {
                yield return null; // de man tieu de kip ve 1 frame truoc khi bi chan
                _collection.WarmUp();
            }
            report(1f);
        }
    }
}
