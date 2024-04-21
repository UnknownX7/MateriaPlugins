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
    private int selectedCraftIndex = -1;
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
                GameInterop.TapButton(synthesisBulkReceiveModalPresenter.NativePtr->view->acceptButton, false);
                return;
            }

            var native = synthesisTopScreenPresenter.NativePtr;
            foreach (var p in native->synthesisContentGroup->nowSynthesisContent->displayCellPresenterArray->PtrEnumerable)
            {
                var i = p.ptr->cellModel->craftIndex;
                if (i >= 0 && i < disabledSynthSlots.Length && disabledSynthSlots[i]) continue;

                switch (p.ptr->view->currentViewType)
                {
                    case SynthesisViewType.Empty:
                        selectedCraftIndex = i;
                        GameInterop.TapButton(p.ptr->view->decideButton, false);
                        return;
                    case SynthesisViewType.Acceptance:
                        GameInterop.TapButton(native->view->bulkReceiveButton, false);
                        return;
                }
            }
            return;
        }
        else if (ScreenManager.Instance?.GetCurrentScreen<SynthesisSelectScreenPresenter>() is { } synthesisSelectScreenPresenter)
        {
            if (!repeatedCraft)
            {
                if (synthesisSelectScreenPresenter.NativePtr->view->repeatCraftButton->isEnable)
                {
                    repeatedCraft = GameInterop.TapButton(synthesisSelectScreenPresenter.NativePtr->view->repeatCraftButton);
                    if (repeatedCraft)
                        waitMs = 750;
                    return;
                }
                else
                {
                    GameInterop.TapButton(synthesisSelectScreenPresenter.NativePtr->header->backButton);
                    if (selectedCraftIndex >= 0 && selectedCraftIndex < disabledSynthSlots.Length)
                        disabledSynthSlots[selectedCraftIndex] = true;
                    return;
                }
            }
            else if (ModalManager.Instance?.GetCurrentModal<GridItemConfirmModal>() is { } gridItemConfirmModal)
            {
                GameInterop.TapButton(gridItemConfirmModal.NativePtr->confirmButton, false);
                return;
            }
            else
            {
                GameInterop.TapButton(synthesisSelectScreenPresenter.NativePtr->view->craftButton, false);
                return;
            }
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
            selectedCraftIndex = -1;
        }
        ImGui.End();
    }
}