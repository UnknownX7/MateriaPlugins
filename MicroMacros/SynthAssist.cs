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

    public void Update()
    {
        if (ModalManager.Instance?.GetCurrentModal<SimpleModalPresenter>() != null) // Inventory full, other general errors
        {
            enabled = false;
            return;
        }

        if (ScreenManager.Instance?.GetCurrentScreen<SynthesisTopScreenPresenter>() is { } synthesisTopScreenPresenter)
        {
            if (ModalManager.Instance?.GetCurrentModal<SynthesisBulkReceiveModalPresenter>() is { } synthesisBulkReceiveModalPresenter)
            {
                GameInterop.TapButton(synthesisBulkReceiveModalPresenter.NativePtr->view->acceptButton, false);
                return;
            }

            var native = synthesisTopScreenPresenter.NativePtr;
            var synthesisArray = native->synthesisContentGroup->nowSynthesisContent->displayCellPresenterArray;
            for (int i = 0; i < synthesisArray->size; i++)
            {
                var synth = synthesisArray->GetPtr(i);
                switch (synth->view->currentViewType)
                {
                    case SynthesisViewType.Empty:
                        GameInterop.TapButton(synth->view->decideButton, false);
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
            if (ModalManager.Instance?.GetCurrentModal<GridItemConfirmModal>() is { } gridItemConfirmModal)
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
        ImGui.Checkbox("Automate Synthesis", ref enabled);
        ImGui.End();
    }
}