#pragma once

#include "NavigationGridComponent.h"

#include <chrono>
#include <memory>
#include <optional>
#include <unordered_map>
#include <vector>

class NavigationEditorTool
{
public:
    enum class BrushShape { Square, Circle };

    void SetTarget(const std::shared_ptr<NavigationGridComponent>& target);
    void EnterEditMode();
    void ExitEditMode();
    bool IsActive() const noexcept { return _active; }

    void Update();
    void RenderInspector();
    void RenderEditorWindow();
    void RenderOverlay();

private:
    struct NavCellChange
    {
        uint32_t index = 0;
        NavCellType before = NavCellType::Walkable;
        NavCellType after = NavCellType::Walkable;
    };

    struct NavPaintCommand
    {
        std::vector<NavCellChange> changes;
    };

    bool UpdateHoveredCell();
    void PaintLine(const NavGridCoordinate& from, const NavGridCoordinate& to);
    void PaintBrush(const NavGridCoordinate& center);
    void PaintCell(int32_t x, int32_t z);
    void BeginStroke();
    void EndStroke();
    void Undo();
    void Redo();
    void ClearHistory();
    void SynchronizeTextBuffers();
    void Save();
    void Reload(bool discardDirty);
    void DrawProjectedQuad(ImDrawList* drawList, int32_t x, int32_t z, ImU32 color, float displayY) const;
    bool Project(const Vec3& world, ImVec2& screen) const;
    std::vector<NavGridCoordinate> GetBrushCells(const NavGridCoordinate& center) const;

    std::shared_ptr<NavigationGridComponent> _target;
    bool _active = false;
    bool _strokeActive = false;
    std::optional<NavGridCoordinate> _hovered;
    std::optional<NavGridCoordinate> _previousStrokeCell;
    NavCellType _paintType = NavCellType::Blocked;
    BrushShape _brushShape = BrushShape::Square;
    int _brushRadius = 0;

    NavPaintCommand _currentStroke;
    std::unordered_map<uint32_t, size_t> _currentChangeLookup;
    std::vector<NavPaintCommand> _undoStack;
    std::vector<NavPaintCommand> _redoStack;
    static constexpr size_t MaximumUndoCommands = 128;

    char _mapIdBuffer[129]{};
    char _assetPathBuffer[512]{};
    char _serverExportPathBuffer[512]{};
    uint32_t _createWidth = 145;
    uint32_t _createHeight = 145;
    float _createCellSize = 1.0f;
    float _createAgentRadius = 0.5f;
    Vec3 _createOrigin = Vec3(0.0f, 0.0f, 0.0f);
    std::string _uiMessage;
    double _lastOverlayBuildMilliseconds = 0.0;
    size_t _lastOverlayFilledCells = 0;
};
