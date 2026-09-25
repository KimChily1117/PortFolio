#include "pch.h"
#include "AnnieQEffect.h"
#include "AnnieWEffect.h"
#include "AnnieSkillTuning.h"
#include "AnniePlayerController.h"
#include "SkillIndicatorController.h"
#include "AnnieOtherPlayerController.h"
#include "GarenOtherPlayerController.h"
#include "ClientPacketHandler.h"
#include "Material.h"
#include "ProjectileScript.h"
#include "ProjectileVisualPolicy.h"
#include "Camera.h"
#include "MeshRenderer.h"
#include "InstancingManager.h"
#include <filesystem>
#include <fstream>
#include <wincodec.h>
#include <limits>

// Exercise the production movement state machine without sending gameplay packets.
class CombatMovementProbe : public PlayerController
{
public:
    void Begin(PlayerState state) { _currentState = state; BeginActionAnimation(); }
    void Finish() { FinishActionAnimation(); }
    void TickMovement() { BasePlayerController::Update(); }
};

// Debug-only entry point. Uses the real renderer on a hidden window, no server
// connection and no game state. Output: Tests/AnnieQ/results/*.png + report.txt.
int RunAnnieQSmokeTest()
{
    const std::filesystem::path output = L"../Tests/AnnieQ/results";
    std::filesystem::create_directories(output);
    std::ofstream report(output / "report.txt");
    int failures = 0;
    auto expect = [&](bool condition, const char* label) {
        report << (condition ? "PASS " : "FAIL ") << label << std::endl;
        if (!condition) ++failures;
    };
    Protocol::S_ProjectileSpawn packet, parsed;
    expect(!UsesAnnieQEffect(Protocol::PLAYER_TYPE_ANNIE, packet), "legacy absent skillId stays legacy");
    packet.set_skillid(0);
    expect(parsed.ParseFromString(packet.SerializeAsString()) && parsed.has_skillid() && parsed.skillid() == 0,
        "explicit basic attack survives protobuf roundtrip");
    expect(!UsesAnnieQEffect(Protocol::PLAYER_TYPE_ANNIE, parsed), "basic attack does not select Q");
    packet.set_skillid(1);
    expect(parsed.ParseFromString(packet.SerializeAsString()) && UsesAnnieQEffect(Protocol::PLAYER_TYPE_ANNIE, parsed),
        "Q roundtrip selects Q");
    expect(!UsesAnnieQEffect(Protocol::PLAYER_TYPE_GAREN, parsed), "Garen never selects Annie Q");
    packet.set_skillid(2);
    expect(!UsesAnnieQEffect(Protocol::PLAYER_TYPE_ANNIE, packet), "other spell does not select Q");
    for (const char* name : { "server-basic", "server-q", "server-legacy" })
    {
        std::ifstream file(output / (std::string(name) + ".packet"), std::ios::binary);
        const std::string wire((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
        expect(file.is_open() && parsed.ParseFromString(wire) &&
            UsesAnnieQEffect(Protocol::PLAYER_TYPE_ANNIE, parsed) == (std::string(name) == "server-q"), name);
    }

    CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    HWND window = CreateWindowExW(0, L"STATIC", L"Annie Q render test", WS_POPUP,
        0, 0, 640, 360, nullptr, nullptr, GetModuleHandleW(nullptr), nullptr);
    expect(window != nullptr, "hidden render window");
    if (!window) return 1;
    auto& desc = GAME->GetGameDesc();
    desc.width = 640; desc.height = 360;
    desc.hWnd = window;
    desc.clearColor = Color(.08f, .1f, .12f, 1.f);
    GRAPHICS->Init(window);
    RESOURCES->Init();
    auto scene = make_shared<Scene>();
    SCENE->ChangeScene(scene);
    auto camera = make_shared<GameObject>("QTestCamera");
    camera->GetOrAddTransform()->SetPosition(Vec3(0.f, 4.f, -7.f));
    camera->GetTransform()->SetRotation(Vec3(.4f, 0.f, 0.f));
    camera->AddComponent(make_shared<Camera>());
    scene->Add(camera);
    camera->GetCamera()->Update();
    auto effects = AnnieQEffect::Get();
    auto projectile = make_shared<GameObject>("QTestProjectile");
    projectile->GetOrAddTransform()->SetPosition(Vec3(-2.5f, 1.f, 0.f));
    auto script = projectile->GetOrAddScript<ProjectileScript>();
    scene->Add(projectile);
    script->SetTarget(Vec3(2.5f, 1.f, 0.f), 6.55f, nullptr, true);
    TIME->Init();
    auto capture = [&](const wchar_t* name) {
        ComPtr<ID3D11RenderTargetView> target;
        DC->OMGetRenderTargets(1, target.GetAddressOf(), nullptr);
        ComPtr<ID3D11Resource> resource;
        target->GetResource(resource.GetAddressOf());
        ScratchImage pixels;
        const HRESULT hr = CaptureTexture(DEVICE.Get(), DC.Get(), resource.Get(), pixels);
        return SUCCEEDED(hr) && SUCCEEDED(SaveToWICFile(*pixels.GetImage(0, 0, 0), WIC_FLAGS_NONE,
            GUID_ContainerFormatPng, (output / name).c_str()));
    };
    float elapsed = 0.f;
    bool hit = false;
    auto captureBright = [&](const wchar_t* name) {
        const auto previous = desc.clearColor;
        desc.clearColor = Color(.28f, .45f, .24f, 1.f);
        GRAPHICS->RenderBegin(); scene->Render();
        const bool saved = capture(name);
        desc.clearColor = previous;
        return saved;
    };
    bool flightImage = false, hitImage = false, smokeImage = false, lateQImage = false;
    while (elapsed < 2.8f)
    {
        Sleep(8);
        TIME->Update();
        elapsed += TIME->GetDeltaTime();
        script->Update();
        if (!hit && elapsed >= .8f)
        {
            script->OnHit(Vec3(2.5f, 1.f, 0.f));
            script->ResetForPool();
            projectile->GetTransform()->SetPosition(Vec3(1000.f)); // pooled owner reused
            hit = true;
        }
        effects->LateUpdate();
        GRAPHICS->RenderBegin();
        scene->Render();
        if (!flightImage && elapsed >= .42f)
        {
            expect(capture(L"flight.png"), "flight GPU capture");
            flightImage = true;
            size_t coreCount = 0;
            for (auto& object : scene->GetObjects())
                if (object->_name == "AnnieQCore") ++coreCount;
            expect(coreCount == 1, "one baked mesh core visible in flight");
            expect(captureBright(L"flight-bright.png"), "Q density on bright background GPU capture");
        }
        if (!hitImage && elapsed >= .86f)
        {
            expect(capture(L"impact.png"), "impact GPU capture");
            hitImage = true;
        }
        if (!smokeImage && elapsed >= 1.1f)
        {
            expect(capture(L"afterglow.png"), "afterglow GPU capture");
            smokeImage = true;
        }
        if (!lateQImage && elapsed >= 1.7f)
        {
            expect(capture(L"impact-extended.png"), "Q impact persists beyond old lifetime GPU capture");
            lateQImage = true;
        }
    }
    size_t visible = 0;
    for (auto& object : scene->GetObjects())
        if (object->_name == "AnnieQCore" || object->_name == "AnnieQBatch") ++visible;
    expect(visible == 0, "all batches retire after hit despite pooled anchor reuse");

    vector<uint64> ids;
    for (int i = 0; i < 20; ++i)
        ids.push_back(effects->StartFlight(projectile, Vec3(1.f, 0.f, 0.f)));
    expect(std::count(ids.begin(), ids.end(), uint64(0)) == 4, "flight capacity capped at 16");
    for (uint64 id : ids) effects->StopFlight(id);
    script->ResetForPool();

    auto w = AnnieWEffect::Get();
    expect(!w->Play(Vec3::Zero, Vec3::Zero), "W rejects zero direction");
    expect(!w->Play(Vec3(std::numeric_limits<float>::quiet_NaN(), 0.f, 0.f), Vec3(0,0,1)),
        "W rejects non-finite origin");
    camera->GetTransform()->SetPosition(Vec3(0.f, 6.f, -5.f));
    camera->GetTransform()->SetRotation(Vec3(.7f, 0.f, 0.f));
    camera->GetCamera()->Update();
    Vec3 wOrigin = Vec3::Zero, wDirection(0.f, 0.f, 1.f);
    expect(w->Play(wOrigin, wDirection), "W accepts baked assets and forward cast");
    wOrigin = Vec3(1000.f); wDirection = Vec3(1.f, 0.f, 0.f);
    elapsed = 0.f;
    bool wBurst = false, wPeak = false, wSustain = false, wGround = false;
    TIME->Init();
    while (elapsed < 3.55f)
    {
        Sleep(8); TIME->Update(); elapsed += TIME->GetDeltaTime();
        w->LateUpdate();
        GRAPHICS->RenderBegin(); scene->Render();
        if (!wBurst && elapsed >= .18f)
        {
            expect(capture(L"w-burst.png"), "W burst GPU capture");
            size_t surfaces = 0;
            bool fixedOrigin = true;
            for (auto& object : scene->GetObjects())
                if (object->_name == "AnnieWSurface")
                {
                    ++surfaces;
                    fixedOrigin &= object->GetTransform()->GetPosition().LengthSquared() < 1.f;
                }
            expect(surfaces == 6 && fixedOrigin, "W six surfaces stay at copied cast origin");
            wBurst = true;
        }
        if (!wPeak && elapsed >= .32f)
        {
            expect(capture(L"w-peak.png"), "W expanded cone GPU capture");
            expect(captureBright(L"w-peak-bright.png"), "W density on bright background GPU capture");
            wPeak = true;
        }
        if (!wSustain && elapsed >= 1.55f)
        {
            expect(capture(L"w-sustain.png"), "W sustained flame GPU capture");
            wSustain = true;
        }
        if (!wGround && elapsed >= 2.65f)
        {
            expect(capture(L"w-ground.png"), "W residual heat GPU capture");
            wGround = true;
        }
    }
    visible = 0;
    for (auto& object : scene->GetObjects())
        if (object->_name == "AnnieWSurface" || object->_name == "AnnieWSparks") ++visible;
    expect(visible == 0, "W all surfaces and sparks retire");
    const Vec3 origins[] = { Vec3(-2,0,1), Vec3(2,0,1), Vec3(2,0,-1), Vec3(-2,0,-1) };
    const Vec3 directions[] = { Vec3(0,0,1), Vec3(1,0,0), Vec3(0,0,-1), Vec3(-1,0,0) };
    bool four = true;
    for (int i = 0; i < 4; ++i) four &= w->Play(origins[i], directions[i]);
    expect(four && !w->Play(Vec3::Zero, Vec3(0,0,1)), "W four simultaneous cardinal casts, bounded capacity");
    camera->GetTransform()->SetPosition(Vec3(0.f, 12.f, -9.f));
    camera->GetTransform()->SetRotation(Vec3(.92f, 0.f, 0.f));
    camera->GetCamera()->Update();
    elapsed = 0.f;
    while (elapsed < .27f)
    {
        Sleep(8); TIME->Update(); elapsed += TIME->GetDeltaTime(); w->LateUpdate();
    }
    GRAPHICS->RenderBegin(); scene->Render();
    expect(capture(L"w-four-directions.png"), "W cardinal directions GPU capture");
    auto newScene = make_shared<Scene>();
    SCENE->ChangeScene(newScene);
    expect(AnnieQEffect::Get() != effects, "effects reset on scene transition");
    auto newW = AnnieWEffect::Get();
    expect(newW != w, "W resets on scene transition");
    newScene->_terrain = make_shared<GameObject>("TestGround");
    newScene->_terrain->GetOrAddTransform()->SetPosition(Vec3(0.f, 2.f, 0.f));
    auto remote = make_shared<GameObject>("RemoteAnnie");
    remote->GetOrAddTransform()->SetPosition(Vec3(10.f, 1.6f, 10.f));
    auto controller = remote->GetOrAddScript<AnnieOtherPlayerController>();
    controller->_playerInfo = make_shared<Protocol::ObjectInfo>();
    controller->_playerInfo->set_objectid(991);
    controller->_playerInfo->set_champtype(Protocol::PLAYER_TYPE_ANNIE);
    auto previousTarget = make_shared<GameObject>("PreviousAttackTarget");
    previousTarget->GetOrAddTransform()->SetPosition(Vec3(10.f, 1.6f, 15.f));
    controller->SetTarget(previousTarget);
    newScene->RegisterObject(991, remote);
    Protocol::S_SkillResult result;
    result.set_casterid(991); result.set_skillid(2);
    result.mutable_castorigin()->set_x(3.f);
    result.mutable_castorigin()->set_y(1.6f);
    result.mutable_castorigin()->set_z(4.f);
    result.mutable_castdirection()->set_x(1.f);
    auto wire = ClientPacketHandler::MakeSendBuffer(result, S_SKILL_RESULT);
    ClientPacketHandler::Handle_S_SkillResult(wire->Buffer(), wire->WriteSize());
    ClientPacketHandler::Handle_S_SkillResult(wire->Buffer(), wire->WriteSize());
    // Local Euler storage uses radians; the legacy world getter decomposes to degrees.
    expect(fabsf(remote->GetTransform()->GetLocalRotation().y - XM_PI * 1.5f) < .001f,
        "W animation faces cast direction despite previous attack target");
    remote->GetTransform()->SetPosition(Vec3(1000.f));
    Sleep(120); TIME->Update(); newW->LateUpdate();
    size_t count = 0;
    bool fixed = true;
    for (auto& object : newScene->GetObjects())
        if (object->_name == "AnnieWSurface")
        {
            ++count;
            const Vec3 pos = object->GetTransform()->GetPosition();
            const auto& material = object->GetMeshRenderer()->GetMaterial()->GetMaterialDesc();
            fixed &= pos.x == 3.f && pos.z == 4.f && pos.y > 2.f && pos.y < 2.3f &&
                material.emissive.x == 1.f && material.emissive.y == 0.f;
        }
    expect(count == 6, "W packet dispatch plays once, duplicate packet suppressed");
    expect(fixed, "W packet origin/direction retained after move, projected above terrain");
    auto garen = make_shared<GameObject>("RemoteGaren");
    garen->GetOrAddTransform();
    auto garenController = garen->GetOrAddScript<GarenOtherPlayerController>();
    garenController->_playerInfo = make_shared<Protocol::ObjectInfo>();
    garenController->_playerInfo->set_objectid(992);
    garenController->_playerInfo->set_champtype(Protocol::PLAYER_TYPE_GAREN);
    newScene->RegisterObject(992, garen);
    result.set_casterid(992);
    wire = ClientPacketHandler::MakeSendBuffer(result, S_SKILL_RESULT);
    ClientPacketHandler::Handle_S_SkillResult(wire->Buffer(), wire->WriteSize());
    Sleep(15); TIME->Update(); newW->LateUpdate();
    count = 0;
    for (auto& object : newScene->GetObjects())
        if (object->_name == "AnnieWSurface") ++count;
    expect(count == 6, "Garen W packet does not start Annie W visuals");

    // Use the same skill-selection entry point as Q/W hotkeys, with the real
    // indicator renderer. No synthetic desktop input or network is needed.
    auto guideScene = make_shared<Scene>();
    SCENE->ChangeScene(guideScene);
    guideScene->_terrain = make_shared<GameObject>("GuideTestGround");
    guideScene->_terrain->GetOrAddTransform()->SetPosition(Vec3(0.f, 2.f, 0.f));
    camera->GetTransform()->SetPosition(Vec3(0.f, 18.f, -14.f));
    camera->GetTransform()->SetRotation(Vec3(atan2f(16.f, 14.f), 0.f, 0.f));
    guideScene->Add(camera);
    camera->GetCamera()->Update();
    auto guideObject = make_shared<GameObject>("AnnieSkillRangeIndicator");
    guideObject->GetOrAddTransform();
    auto guide = guideObject->GetOrAddScript<SkillIndicatorController>();
    guideScene->Add(guideObject);
    auto previousGuide = UI->GetSkillIndicatorController();
    UI->SetSkillIndicatorControllerGameObject(guideObject);
    auto local = make_shared<GameObject>("LocalAnnieGuideTest");
    local->GetOrAddTransform()->SetPosition(Vec3(0.f, 1.6f, 0.f));
    auto localController = local->GetOrAddScript<AnniePlayerController>();
    localController->_playerInfo = make_shared<Protocol::ObjectInfo>();
    localController->_playerInfo->set_champtype(Protocol::PLAYER_TYPE_ANNIE);
    localController->_playerInfo->set_hp(100);
    localController->SelectSkill((int32)SkillType::QSpell);
    expect(guide->IsVisible() && guide->IsCircle() && guide->GetRange() == 8.f &&
        guideObject->GetTransform()->GetLocalScale().x == 16.f,
        "Q selection shows radius 8 matching existing cast distance");
    GRAPHICS->RenderBegin(); guideScene->Render();
    expect(capture(L"q-range.png"), "Q range circle GPU capture");
    local->GetTransform()->SetPosition(Vec3(2.f, 1.6f, 1.f));
    guide->Update();
    const auto guidePosition = guideObject->GetTransform()->GetPosition();
    expect(fabsf(guidePosition.x - 2.f) < .001f && fabsf(guidePosition.z - 1.f) < .001f &&
        fabsf(guidePosition.y - 2.035f) < .001f, "Q circle follows moving owner above terrain");
    local->GetTransform()->SetPosition(Vec3(0.f, 1.6f, 0.f));
    localController->SelectSkill((int32)SkillType::WSpell);
    guide->SetAimPosition(Vec3(0.f, 2.f, 5.6f)); guide->Update();
    expect(guide->IsVisible() && !guide->IsCircle() && fabsf(guide->GetRange() - 5.6f) < .001f &&
        fabsf(guideObject->GetTransform()->GetLocalScale().y - 5.6f) < .001f,
        "W selection replaces Q with radius 5.6 sector");
    GRAPHICS->RenderBegin(); guideScene->Render();
    expect(capture(L"w-range.png"), "enlarged W cone GPU capture");
    localController->SelectSkill((int32)SkillType::QSpell);
    expect(guide->IsVisible() && guide->IsCircle(), "Q replaces W targeting without stale cone");
    localController->SelectSkill(0);
    expect(!guide->IsVisible() && guideObject->GetTransform()->GetLocalScale() == Vec3::Zero,
        "cancel hides range indicator");
    localController->SelectSkill((int32)SkillType::QSpell);
    localController->ConfirmSkillCooldown((int32)SkillType::QSpell);
    localController->SelectSkill((int32)SkillType::QSpell);
    expect(!guide->IsVisible(), "Q cooldown does not show a castable range indicator");
    localController->GetCooldowns()[(int32)SkillType::QSpell] = 5.f;
    localController->SelectSkill((int32)SkillType::QSpell);
    localController->_playerInfo->set_hp(0);
    guide->Update();
    expect(!guide->IsVisible(), "range indicator hides when owner dies");
    localController->_playerInfo->set_hp(100);
    localController->SelectSkill((int32)SkillType::WSpell);
    guide->SetAimPosition(Vec3(0.f, 2.f, 5.6f)); guide->Update();
    auto guideW = AnnieWEffect::Get();
    guideW->Play(Vec3(0.f, 1.6f, 0.f), Vec3(0.f, 0.f, 1.f));
    elapsed = 0.f; TIME->Init();
    while (elapsed < .32f)
    {
        Sleep(8); TIME->Update(); elapsed += TIME->GetDeltaTime(); guideW->LateUpdate();
    }
    GRAPHICS->RenderBegin(); guideScene->Render();
    expect(capture(L"w-range-overlay.png"), "W enlarged VFX and guide alignment GPU capture");
    UI->SetSkillIndicatorControllerGameObject(previousGuide);

    auto movementObject = make_shared<GameObject>("CombatMovementProbe");
    movementObject->GetOrAddTransform()->SetPosition(Vec3(0.f, 1.6f, 0.f));
    auto movement = movementObject->GetOrAddScript<CombatMovementProbe>();
    movement->_playerInfo = make_shared<Protocol::ObjectInfo>();
    movement->_playerInfo->set_hp(100);
    uint64 moveId = 100;
    for (auto champion : { Protocol::PLAYER_TYPE_ANNIE, Protocol::PLAYER_TYPE_GAREN })
    {
        movement->_playerInfo->set_champtype(champion);
        for (auto state : { PlayerState::ATK1, PlayerState::ATK2, PlayerState::Q,
            PlayerState::W, PlayerState::E, PlayerState::R })
        {
            const bool mobile = champion == Protocol::PLAYER_TYPE_GAREN && state == PlayerState::E;
            const Vec3 start(0.f, 1.6f, 0.f), goal(1.f, 1.6f, 0.f);
            movementObject->GetTransform()->SetPosition(start);
            movement->_dest = goal;
            movement->ApplyAuthoritativeSnapshot(++moveId, 1, goal, Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING);
            movement->Begin(state);
            expect(movement->IsMovementLocked() == !mobile, "attack/skill movement policy, only Garen E is mobile");
            movement->OnMoveAccepted(moveId, goal, false);
            movement->ApplyAuthoritativeSnapshot(moveId, 2, goal, Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING);
            Sleep(15); TIME->Update(); movement->TickMovement();
            expect((movementObject->GetTransform()->GetPosition() != start) == mobile,
                "active action blocks stale path interpolation except Garen E");
            expect((movement->_dest == goal) == mobile, "late movement acceptance cannot restore cancelled destination");
            expect(movement->_currentState == state, "movement snapshot preserves action animation including Garen E");
            expect(movement->ApplyAuthoritativeSnapshot(moveId, 2, start, Protocol::MOVEMENT_SNAPSHOT_STATE_CANCELLED),
                "same-tick cancellation follows moving snapshot");
            expect(movementObject->GetTransform()->GetPosition() == start, "cancel applies final authoritative position immediately");
            movement->Finish();
            expect(!movement->IsMovementLocked() && movement->_currentState == PlayerState::IDLE,
                "action completion releases movement without resuming cancelled path");
            expect(!movement->ApplyAuthoritativeSnapshot(moveId, 3, goal, Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING),
                "late snapshot cannot resurrect completed move ID");
            expect(movement->ApplyAuthoritativeSnapshot(++moveId, 1, goal, Protocol::MOVEMENT_SNAPSHOT_STATE_MOVING),
                "new movement accepted after action completion");
        }
    }
    movement->_playerInfo->set_champtype(Protocol::PLAYER_TYPE_GAREN);
    movement->Begin(PlayerState::E);
    const Vec3 standing = movementObject->GetTransform()->GetPosition();
    movement->_dest = Vec3(20.f, 1.6f, 20.f);
    movement->MoveTo();
    expect(movementObject->GetTransform()->GetPosition() == standing,
        "stationary Garen E does not invent a legacy movement destination");
    movement->Finish();
    movement->_playerInfo->set_champtype(Protocol::PLAYER_TYPE_ANNIE);
    movement->HoldMovementForSkillRequest();
    expect(movement->IsMovementLocked() && movement->IsActionBusy(), "W request locks movement while awaiting server approval");
    movement->Begin(PlayerState::W);
    movement->Finish();
    expect(!movement->IsActionBusy(), "approved W releases pending request on completion");
    movement->HoldMovementForSkillRequest();
    Sleep(2050); TIME->Update();
    expect(!movement->IsMovementLocked(), "rejected or unanswered W request has bounded movement lock");
    movement->Begin(PlayerState::Q);
    movement->_currentState = PlayerState::DIE;
    movement->_playerInfo->set_hp(0);
    movement->Finish();
    expect(movement->_currentState == PlayerState::DIE, "finishing action does not revive death animation");

    // Real Annie W approval path also consumes the pending movement lock.
    localController->HoldMovementForSkillRequest();
    static_pointer_cast<BasePlayerController>(localController)->PlayServerSkillResult(
        (int32)SkillType::WSpell, Vec3::Zero, Vec3(1.f, 0.f, 0.f));
    expect(localController->_currentState == PlayerState::W && localController->IsMovementLocked(),
        "real W approval starts animation and retains cast movement lock");
    localController->GetCooldowns()[(int32)SkillType::QSpell] = 5.f;
    UI->SetSkillIndicatorControllerGameObject(guideObject);
    localController->SelectSkill((int32)SkillType::QSpell);
    expect(!guide->IsVisible(), "skill hotkey cannot select another skill during active action");
    UI->SetSkillIndicatorControllerGameObject(previousGuide);
    report << "failures=" << failures << std::endl;
    DestroyWindow(window);
    return failures == 0 ? 0 : 1;
}
