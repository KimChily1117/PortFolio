#include "pch.h"
#include "NavigationEditorTool.h"
#include "NavigationPaintAlgorithms.h"

#include "Camera.h"
#include "Scene.h"

#include <algorithm>
#include <cmath>
#include <cstring>

namespace
{
    const char* CellTypeName(NavCellType type)
    {
        switch (type)
        {
        case NavCellType::Walkable: return "Walkable";
        case NavCellType::Blocked: return "Blocked";
        case NavCellType::Slow: return "Slow";
        default: return "Unknown";
        }
    }

    void CopyText(char* destination, size_t capacity, const std::string& value)
    {
        if (capacity == 0) return;
        const size_t count = (std::min)(capacity - 1, value.size());
        std::memcpy(destination, value.data(), count);
        destination[count] = '\0';
    }
}

void NavigationEditorTool::SetTarget(const std::shared_ptr<NavigationGridComponent>& target)
{
    if (_target == target)
        return;
    EndStroke();
    _target = target;
    _hovered.reset();
    ClearHistory();
    SynchronizeTextBuffers();
}

void NavigationEditorTool::EnterEditMode()
{
    if (!_target || !_target->HasGrid())
    {
        _uiMessage = "Create or load a NavGrid before entering edit mode.";
        return;
    }
    _active = true;
    _target->showGrid = true;
    _uiMessage = "Navigation Paint mode enabled. World selection and MoveRequest input are blocked.";
}

void NavigationEditorTool::ExitEditMode()
{
    EndStroke();
    _active = false;
    _hovered.reset();
    _previousStrokeCell.reset();
    _uiMessage = _target && _target->IsDirty()
        ? "Exited Navigation Paint mode. Unsaved edits are retained."
        : "Exited Navigation Paint mode.";
}

void NavigationEditorTool::Update()
{
    if (!_active || !_target || !_target->HasGrid())
        return;

    if (INPUT->GetButtonDown(KEY_TYPE::ESCAPE))
    {
        ExitEditMode();
        return;
    }

    if (INPUT->GetButton(KEY_TYPE::CONTROL) || INPUT->GetButtonDown(KEY_TYPE::CONTROL))
    {
        if (INPUT->GetButtonDown(KEY_TYPE::Z)) Undo();
        if (INPUT->GetButtonDown(KEY_TYPE::Y)) Redo();
    }

    const bool hovered = UpdateHoveredCell();
    const bool uiCapturesMouse = ImGui::GetIO().WantCaptureMouse;

    if (INPUT->GetButtonDown(KEY_TYPE::LBUTTON) && hovered && !uiCapturesMouse)
    {
        BeginStroke();
        PaintBrush(*_hovered);
        _previousStrokeCell = _hovered;
    }
    else if (_strokeActive && INPUT->GetButton(KEY_TYPE::LBUTTON) && hovered && !uiCapturesMouse)
    {
        if (_previousStrokeCell)
            PaintLine(*_previousStrokeCell, *_hovered);
        else
            PaintBrush(*_hovered);
        _previousStrokeCell = _hovered;
    }

    if (_strokeActive && INPUT->GetButtonUp(KEY_TYPE::LBUTTON))
        EndStroke();
}

bool NavigationEditorTool::UpdateHoveredCell()
{
    _hovered.reset();
    const POINT point = INPUT->GetMousePos();
    const float width = GRAPHICS->GetViewport().GetWidth();
    const float height = GRAPHICS->GetViewport().GetHeight();
    if (point.x < 0 || point.y < 0 || point.x >= width || point.y >= height)
        return false;

    Ray ray;
    if (!CUR_SCENE->TryCreatePickingRay(point.x, point.y, ray))
        return false;

    float planeY = _target->GetTransform()->GetPosition().y;
    const DirectX::SimpleMath::Plane plane(Vec3::UnitY, -planeY);
    float distance = 0.0f;
    if (!ray.Intersects(plane, distance) || distance < 0.0f)
        return false;

    const Vec3 position = ray.position + ray.direction * distance;
    _hovered = _target->WorldToCell(position);
    return _hovered.has_value();
}

void NavigationEditorTool::BeginStroke()
{
    _strokeActive = true;
    _currentStroke.changes.clear();
    _currentChangeLookup.clear();
    _previousStrokeCell.reset();
}

void NavigationEditorTool::EndStroke()
{
    if (!_strokeActive)
        return;
    _strokeActive = false;
    _previousStrokeCell.reset();
    if (_currentStroke.changes.empty())
        return;

    _undoStack.push_back(std::move(_currentStroke));
    if (_undoStack.size() > MaximumUndoCommands)
        _undoStack.erase(_undoStack.begin());
    _redoStack.clear();
    _currentStroke = {};
    _currentChangeLookup.clear();
}

void NavigationEditorTool::PaintLine(const NavGridCoordinate& from, const NavGridCoordinate& to)
{
    for (const NavGridCoordinate& cell : NavigationPaintAlgorithms::RasterizeLine(from, to))
        PaintBrush(cell);
}

std::vector<NavGridCoordinate> NavigationEditorTool::GetBrushCells(const NavGridCoordinate& center) const
{
    return NavigationPaintAlgorithms::EnumerateBrush(
        center, _brushRadius, _brushShape == BrushShape::Circle,
        _target->GetData().GetWidth(), _target->GetData().GetHeight());
}

void NavigationEditorTool::PaintBrush(const NavGridCoordinate& center)
{
    for (const NavGridCoordinate& cell : GetBrushCells(center))
        PaintCell(cell.x, cell.z);
}

void NavigationEditorTool::PaintCell(int32_t x, int32_t z)
{
    if (!_target->IsValidCell(x, z))
        return;
    const NavCellType before = _target->GetCellType(x, z);
    if (before == _paintType)
        return;

    const uint32_t index = static_cast<uint32_t>(z) * _target->GetData().GetWidth() + static_cast<uint32_t>(x);
    const auto found = _currentChangeLookup.find(index);
    if (found == _currentChangeLookup.end())
    {
        _currentChangeLookup[index] = _currentStroke.changes.size();
        _currentStroke.changes.push_back({ index, before, _paintType });
    }
    else
    {
        _currentStroke.changes[found->second].after = _paintType;
    }
    _target->SetCellType(x, z, _paintType);
}

void NavigationEditorTool::Undo()
{
    EndStroke();
    if (_undoStack.empty() || !_target) return;
    NavPaintCommand command = std::move(_undoStack.back());
    _undoStack.pop_back();
    const uint32_t width = _target->GetData().GetWidth();
    for (auto it = command.changes.rbegin(); it != command.changes.rend(); ++it)
        _target->SetCellType(it->index % width, it->index / width, it->before);
    _redoStack.push_back(std::move(command));
}

void NavigationEditorTool::Redo()
{
    EndStroke();
    if (_redoStack.empty() || !_target) return;
    NavPaintCommand command = std::move(_redoStack.back());
    _redoStack.pop_back();
    const uint32_t width = _target->GetData().GetWidth();
    for (const NavCellChange& change : command.changes)
        _target->SetCellType(change.index % width, change.index / width, change.after);
    _undoStack.push_back(std::move(command));
}

void NavigationEditorTool::ClearHistory()
{
    _undoStack.clear();
    _redoStack.clear();
    _currentStroke = {};
    _currentChangeLookup.clear();
}

void NavigationEditorTool::SynchronizeTextBuffers()
{
    if (!_target) return;
    CopyText(_mapIdBuffer, sizeof(_mapIdBuffer), _target->GetNavigationMapId());
    CopyText(_assetPathBuffer, sizeof(_assetPathBuffer), _target->GetAssetPath());
    CopyText(_serverExportPathBuffer, sizeof(_serverExportPathBuffer), _target->GetServerExportPath());
}

void NavigationEditorTool::Save()
{
    if (!_target) return;
    _target->SetNavigationMapId(_mapIdBuffer);
    _target->SetAssetPath(_assetPathBuffer);
    std::string error;
    if (!_target->SaveNavGrid(std::filesystem::path(_assetPathBuffer), error))
        _uiMessage = "Save failed: " + error;
    else
        _uiMessage = _target->GetLastStatus();
}

void NavigationEditorTool::Reload(bool discardDirty)
{
    if (!_target || (_target->IsDirty() && !discardDirty))
        return;
    _target->SetAssetPath(_assetPathBuffer);
    std::string error;
    if (!_target->Reload(error))
        _uiMessage = "Reload failed; current grid preserved: " + error;
    else
    {
        ClearHistory();
        SynchronizeTextBuffers();
        _uiMessage = _target->GetLastStatus();
    }
}

void NavigationEditorTool::RenderInspector()
{
    if (!_target) return;
    if (!ImGui::Begin("Navigation Grid Component"))
    {
        ImGui::End();
        return;
    }

    ImGui::InputText("Navigation Map ID", _mapIdBuffer, sizeof(_mapIdBuffer));
    ImGui::InputText("Asset Path", _assetPathBuffer, sizeof(_assetPathBuffer));
    ImGui::InputText("Server Export Path", _serverExportPathBuffer, sizeof(_serverExportPathBuffer));

    if (_target->HasGrid())
    {
        const EditableNavGridData& data = _target->GetData();
        ImGui::Text("Width / Height: %u / %u", data.GetWidth(), data.GetHeight());
        ImGui::Text("Cell Size: %.3f", data.GetCellSize());
        ImGui::Text("Origin: (%.3f, %.3f, %.3f)", data.GetOrigin().x, data.GetOrigin().y, data.GetOrigin().z);
        float agentRadius = data.GetDefaultAgentRadius();
        if (ImGui::InputFloat("Default Agent Radius", &agentRadius, 0.1f, 0.5f, "%.3f"))
        {
            std::string radiusError;
            if (!_target->SetDefaultAgentRadius(agentRadius, radiusError)) _uiMessage = radiusError;
        }
        ImGui::Text("Format Version: 1");
        ImGui::TextWrapped("Saved Content Hash: %s", _target->GetContentHashHex().empty() ? "(unsaved)" : _target->GetContentHashHex().c_str());
        if (_target->IsDirty()) ImGui::TextColored(ImVec4(1, 0.75f, 0, 1), "DIRTY - unsaved Navigation changes");
    }

    ImGui::Separator();
    ImGui::InputScalar("Create Width", ImGuiDataType_U32, &_createWidth);
    ImGui::InputScalar("Create Height", ImGuiDataType_U32, &_createHeight);
    ImGui::InputFloat("Create Cell Size", &_createCellSize, 0.1f, 1.0f, "%.3f");
    ImGui::InputFloat("Create Agent Radius", &_createAgentRadius, 0.1f, 0.5f, "%.3f");
    ImGui::InputFloat3("Create Origin", &_createOrigin.x, "%.3f");
    if (ImGui::Button("Create / Recreate Grid"))
    {
        _target->SetNavigationMapId(_mapIdBuffer);
        std::string error;
        if (_target->CreateGrid(_createWidth, _createHeight, _createCellSize, _createOrigin, error))
        {
            std::string radiusError;
            _target->SetDefaultAgentRadius(_createAgentRadius, radiusError);
            ClearHistory();
            _uiMessage = radiusError.empty() ? _target->GetLastStatus() : radiusError;
        }
        else _uiMessage = "Create failed: " + error;
    }
    ImGui::SameLine();
    if (ImGui::Button("Load"))
    {
        if (_target->IsDirty()) _uiMessage = "Load blocked: use 'Discard Edits & Reload' to confirm data loss.";
        else Reload(true);
    }
    ImGui::SameLine();
    if (ImGui::Button("Reload")) Reload(false);
    if (_target->IsDirty())
    {
        ImGui::SameLine();
        if (ImGui::Button("Discard Edits & Reload")) Reload(true);
    }

    if (ImGui::Button("Save / Save As")) Save();
    ImGui::SameLine();
    if (ImGui::Button("Export Server Content"))
    {
        _target->SetServerExportPath(_serverExportPathBuffer);
        std::string error;
        if (!_target->ExportServerContent(std::filesystem::path(_serverExportPathBuffer), error))
            _uiMessage = "Export failed: " + error;
        else _uiMessage = _target->GetLastStatus() + ". Update navigation-maps.json contentHash explicitly.";
    }
    ImGui::SameLine();
    if (!_active)
    {
        if (ImGui::Button("Enter Navigation Edit Mode")) EnterEditMode();
    }
    else if (ImGui::Button("Exit Navigation Edit Mode")) ExitEditMode();

    ImGui::Checkbox("Show Grid", &_target->showGrid);
    ImGui::SameLine(); ImGui::Checkbox("Walkable", &_target->showWalkable);
    ImGui::SameLine(); ImGui::Checkbox("Blocked", &_target->showBlocked);
    ImGui::SameLine(); ImGui::Checkbox("Slow", &_target->showSlow);
    ImGui::Checkbox("Cell Borders", &_target->showCellBorders);

    if (!_uiMessage.empty()) ImGui::TextWrapped("%s", _uiMessage.c_str());
    ImGui::End();
}

void NavigationEditorTool::RenderEditorWindow()
{
    if (!_active || !_target) return;
    if (!ImGui::Begin("Navigation Editor"))
    {
        ImGui::End();
        return;
    }

    ImGui::Text("Target: %s", _target->GetGameObject()->_name.c_str());
    ImGui::Text("Map: %s", _target->GetNavigationMapId().c_str());
    ImGui::Text("State: %s", _target->IsDirty() ? "DIRTY" : "Saved");
    const char* paintItems[] = { "Walkable", "Blocked", "Slow" };
    int paint = static_cast<int>(_paintType);
    if (ImGui::Combo("Paint Type", &paint, paintItems, IM_ARRAYSIZE(paintItems)))
        _paintType = static_cast<NavCellType>(paint);
    const char* shapeItems[] = { "Square", "Circle" };
    int shape = static_cast<int>(_brushShape);
    if (ImGui::Combo("Brush Shape", &shape, shapeItems, IM_ARRAYSIZE(shapeItems)))
        _brushShape = static_cast<BrushShape>(shape);
    ImGui::SliderInt("Brush Radius", &_brushRadius, 0, 20);

    if (_hovered)
    {
        const auto center = _target->CellToWorldCenter(*_hovered);
        ImGui::Text("Hovered: (%d, %d) %s", _hovered->x, _hovered->z,
            CellTypeName(_target->GetCellType(_hovered->x, _hovered->z)));
        if (center) ImGui::Text("Center: (%.2f, %.2f, %.2f)", center->x, center->y, center->z);
        ImGui::Text("Brush Cells: %zu", GetBrushCells(*_hovered).size());
    }
    else ImGui::Text("Hovered: outside grid");

    size_t counts[3] = {};
    for (uint8_t value : _target->GetData().GetCells()) ++counts[value];
    ImGui::Text("Cells W/B/S: %zu / %zu / %zu", counts[0], counts[1], counts[2]);
    ImGui::Text("Overlay: %zu fills, %.3f ms CPU build", _lastOverlayFilledCells, _lastOverlayBuildMilliseconds);

    if (ImGui::Button("Undo (Ctrl+Z)")) Undo();
    ImGui::SameLine(); if (ImGui::Button("Redo (Ctrl+Y)")) Redo();
    ImGui::SameLine(); if (ImGui::Button("Save")) Save();
    ImGui::SameLine(); if (ImGui::Button("Exit")) ExitEditMode();
    ImGui::End();
}

bool NavigationEditorTool::Project(const Vec3& world, ImVec2& screen) const
{
    auto cameraObject = CUR_SCENE->GetMainCamera();
    if (!cameraObject || !cameraObject->GetCamera()) return false;
    const Vec3 projected = GRAPHICS->GetViewport().Project(
        world, Matrix::Identity,
        cameraObject->GetCamera()->GetViewMatrix(),
        cameraObject->GetCamera()->GetProjectionMatrix());
    if (!std::isfinite(projected.x) || !std::isfinite(projected.y) || projected.z < 0.0f || projected.z > 1.0f)
        return false;
    screen = ImVec2(projected.x, projected.y);
    return true;
}

void NavigationEditorTool::DrawProjectedQuad(
    ImDrawList* drawList, int32_t x, int32_t z, ImU32 color, float displayY) const
{
    const EditableNavGridData& data = _target->GetData();
    const float x0 = data.GetOrigin().x + x * data.GetCellSize();
    const float z0 = data.GetOrigin().z + z * data.GetCellSize();
    const float x1 = x0 + data.GetCellSize();
    const float z1 = z0 + data.GetCellSize();
    ImVec2 p[4];
    if (Project(Vec3(x0, displayY, z0), p[0]) && Project(Vec3(x1, displayY, z0), p[1]) &&
        Project(Vec3(x1, displayY, z1), p[2]) && Project(Vec3(x0, displayY, z1), p[3]))
    {
        drawList->AddQuadFilled(p[0], p[1], p[2], p[3], color);
    }
}

void NavigationEditorTool::RenderOverlay()
{
    if (!_active || !_target || !_target->HasGrid() || !_target->showGrid)
        return;
    const auto begin = std::chrono::steady_clock::now();
    _lastOverlayFilledCells = 0;
    ImDrawList* drawList = ImGui::GetBackgroundDrawList();
    const EditableNavGridData& data = _target->GetData();
    const float displayY = _target->GetTransform()->GetPosition().y + 0.02f;

    for (uint32_t z = 0; z < data.GetHeight(); ++z)
    {
        for (uint32_t x = 0; x < data.GetWidth(); ++x)
        {
            const NavCellType type = data.GetCell(x, z);
            ImU32 color = 0;
            if (type == NavCellType::Walkable && _target->showWalkable) color = IM_COL32(30, 180, 70, 40);
            if (type == NavCellType::Blocked && _target->showBlocked) color = IM_COL32(230, 45, 45, 115);
            if (type == NavCellType::Slow && _target->showSlow) color = IM_COL32(245, 190, 35, 105);
            if (color)
            {
                DrawProjectedQuad(drawList, x, z, color, displayY);
                ++_lastOverlayFilledCells;
            }
        }
    }

    if (_target->showCellBorders)
    {
        const ImU32 border = IM_COL32(190, 210, 230, 75);
        const float minX = data.GetOrigin().x;
        const float minZ = data.GetOrigin().z;
        const float maxX = minX + data.GetWidth() * data.GetCellSize();
        const float maxZ = minZ + data.GetHeight() * data.GetCellSize();
        for (uint32_t x = 0; x <= data.GetWidth(); ++x)
        {
            const float worldX = minX + x * data.GetCellSize();
            ImVec2 a, b;
            if (Project(Vec3(worldX, displayY, minZ), a) && Project(Vec3(worldX, displayY, maxZ), b))
                drawList->AddLine(a, b, border, 1.0f);
        }
        for (uint32_t z = 0; z <= data.GetHeight(); ++z)
        {
            const float worldZ = minZ + z * data.GetCellSize();
            ImVec2 a, b;
            if (Project(Vec3(minX, displayY, worldZ), a) && Project(Vec3(maxX, displayY, worldZ), b))
                drawList->AddLine(a, b, border, 1.0f);
        }
    }

    if (_hovered)
    {
        for (const NavGridCoordinate& cell : GetBrushCells(*_hovered))
            DrawProjectedQuad(drawList, cell.x, cell.z, IM_COL32(80, 180, 255, 125), displayY + 0.005f);
    }

    const auto end = std::chrono::steady_clock::now();
    _lastOverlayBuildMilliseconds = std::chrono::duration<double, std::milli>(end - begin).count();
}
