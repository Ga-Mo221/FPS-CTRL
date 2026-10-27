// [Aemy 2026-10-26] FILE MOI. Scene Boot (man tieu de truoc Login).
using System;
using System.Collections;

namespace Game.Boot
{
    /// <summary>
    /// 1 buoc khoi dong chay ngam trong luc man tieu de dang hien.
    /// BootSequence chay lan luot cac buoc; them buoc moi = them 1 class moi, khong sua BootSequence (OCP).
    /// </summary>
    public interface IBootStep
    {
        /// <summary>Ten buoc (log / debug).</summary>
        string Label { get; }
        /// <summary>Ty trong cua buoc tren thanh tien trinh (buoc nang -> so lon).</summary>
        float Weight { get; }
        /// <summary>Chay buoc; goi report(0..1) de bao tien do.</summary>
        IEnumerator Run(Action<float> report);
    }
}
