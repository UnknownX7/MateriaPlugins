using ECGen.Generated.Command.OutGame;
using ECGen.Generated.Command.OutGame.Synthesis;
using ImGuiNET;
using Materia.Game;

namespace MicroMacros;

public unsafe class SynthAssist : IMicroMacro
{
    private bool enabled = false;
    public ref bool Enabled => ref enabled;

    public void Update()
    {
        if (ScreenManager.Instance?.GetCurrentScreen<SynthesisTopScreenPresenter>() is { } synthesisTopScreenPresenter)
        {
            if (ModalManager.Instance?.GetCurrentModal<SynthesisBulkReceiveModalPresenter>() is { } synthesisBulkReceiveModalPresenter)
            {
                GameInterop.TapButton(synthesisBulkReceiveModalPresenter.NativePtr->view->acceptButton);
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
                        GameInterop.TapButton(synth->view->decideButton);
                        return;
                    case SynthesisViewType.Acceptance:
                        GameInterop.TapButton(native->view->bulkReceiveButton);
                        return;
                }
            }
            return;
        }
        else if (ScreenManager.Instance?.GetCurrentScreen<SynthesisSelectScreenPresenter>() is { } synthesisSelectScreenPresenter)
        {
            if (ModalManager.Instance?.GetCurrentModal<GridItemConfirmModal>() is { } gridItemConfirmModal)
            {
                GameInterop.TapButton(gridItemConfirmModal.NativePtr->confirmButton);
                return;
            }
            else
            {
                GameInterop.TapButton(synthesisSelectScreenPresenter.NativePtr->view->craftButton);
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