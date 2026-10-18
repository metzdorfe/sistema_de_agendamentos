using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EspacoFestas.Services.Whatsapp;

/// <summary>
/// Job Object do Windows com "matar ao fechar": processos filhos vinculados a ele morrem junto com o sistema,
/// mesmo se o FestAgenda.exe travar ou for encerrado à força (nada de node.exe órfão).
/// O identificador fica aberto enquanto o processo vive; o Windows fecha ao sair e encerra os filhos.
/// </summary>
internal static class JobObject
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint LimitKillOnJobClose = 0x2000;

    private static readonly IntPtr Job = Criar();

    /// <summary>Vincula o processo ao job (se o job não pôde ser criado, segue sem: o servidor ainda sai ao fechar a entrada padrão).</summary>
    public static void Vincular(Process processo)
    {
        if (Job != IntPtr.Zero)
        {
            AssignProcessToJobObject(Job, processo.Handle);
        }
    }

    private static IntPtr Criar()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var info = new ExtendedLimitInformation { BasicLimitInformation = { LimitFlags = LimitKillOnJobClose } };
        return SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<ExtendedLimitInformation>())
            ? job
            : IntPtr.Zero;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr atributos, string? nome);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int classe, ref ExtendedLimitInformation info, uint tamanho);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr processo);

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
