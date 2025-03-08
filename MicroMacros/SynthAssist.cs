using System.Diagnostics;
using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Synthesis;
using ECGen.Generated.Command.UI;
using ImGuiNET;
using Materia.Game;
using ModalManager = Materia.Game.ModalManager;
using ScreenManager = Materia.Game.ScreenManager;

namespace MicroMacros;

public unsafe class SynthAssist : IMicroMacro
{
    private bool enabled = false;
    public ref bool Enabled => ref enabled;

    private bool repeatedCraft = false;
    private bool[] disabledSynthSlots = null!;
    private long waitMs;
    private readonly Stopwatch waitStopwatch = new();
    public void Update()
    {
        if (waitMs > 0)
        {
            if (!waitStopwatch.IsRunning)
                waitStopwatch.Restart();

            if (waitMs > waitStopwatch.ElapsedMilliseconds) return;

            waitMs = 0;
            waitStopwatch.Stop();
        }

        if (ModalManager.Instance?.GetCurrentModal<SimpleModalPresenter>() != null) // Inventory full, other general errors
        {
            enabled = false;
            return;
        }

        if (ScreenManager.Instance?.GetCurrentScreen<SynthesisTopScreenPresenter>() is { } synthesisTopScreenPresenter)
        {
            repeatedCraft = false;

            if (ModalManager.Instance?.GetCurrentModal<SynthesisBulkReceiveModalPresenter>() is { } synthesisBulkReceiveModalPresenter)
            {
                GameInterop.TapButton(synthesisBulkReceiveModalPresenter.NativePtr->view->acceptButton);
                return;
            }

            if (ModalManager.Instance?.GetCurrentModal<GridItemConfirmModal>() is { } gridItemConfirmModal)
            {
                GameInterop.TapButton(gridItemConfirmModal.NativePtr->confirmButton);
                return;
            }

            if (ModalManager.Instance?.GetCurrentModal<BulkSynthesisModalPresenter>() is { } bulkSynthesisModalPresenter)
            {
                foreach (var p in bulkSynthesisModalPresenter.NativePtr->bulkSynthesisContentGroup->nowBulkSynthesisContentModel->bulkSynthesisCellModels->PtrEnumerable)
                {
                    switch (p.ptr->bulkSynthesisViewType->GetValue())
                    {
                        case BulkSynthesisViewType.Synthesis:
                            break;
                        case BulkSynthesisViewType.CanSynthesis:
                            GameInterop.TapButton(bulkSynthesisModalPresenter.NativePtr->bulkSynthesisButton);
                            break;
                        default:
                            disabledSynthSlots[p.ptr->craftIndex] = true;
                            break;
                    }
                }

                GameInterop.TapButton(bulkSynthesisModalPresenter.NativePtr->header->closeButton);
                return;
            }

            var native = synthesisTopScreenPresenter.NativePtr;
            foreach (var p in native->synthesisContentGroup->nowSynthesisContentModel->synthesisCellModels->PtrEnumerable)
            {
                var i = p.ptr->craftIndex;
                if (i >= 0 && i < disabledSynthSlots.Length && disabledSynthSlots[i]) continue;

                switch (p.ptr->synthesisViewType->GetValue())
                {
                    case SynthesisViewType.Empty:
                        GameInterop.TapButton(native->view->historyBulkSynthesisButton);
                        return;
                    case SynthesisViewType.Acceptance:
                        GameInterop.TapButton(native->view->bulkReceiveButton);
                        break;
                }
            }
            return;
        }

        enabled = false;
    }

    public void Draw()
    {
        if (ScreenManager.Instance?.GetCurrentScreen<SynthesisTopScreenPresenter>() == null && ScreenManager.Instance?.GetCurrentScreen<SynthesisSelectScreenPresenter>() == null) return;
        ImGui.Begin("SynthAssist", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDecoration);
        if (ImGui.Checkbox("Automate Synthesis", ref enabled) && enabled)
        {
            repeatedCraft = false;
            disabledSynthSlots = new bool[10];
        }
        ImGui.End();
    }
}