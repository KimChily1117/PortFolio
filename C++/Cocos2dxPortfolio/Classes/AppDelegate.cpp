#include "AppDelegate.h"
#include "GameScene.h"
#include "FrontendScene.h"
#include "ResourceGallery.h"
#include "SpinePreview.h"
#include "AnimatedCutscene.h"
#include "SkillPresentation.h"
#include "ReferenceAudio.h"
#include "AudioLibraryScene.h"
#include <cstdlib>
#include <fstream>
USING_NS_CC;
bool AppDelegate::applicationDidFinishLaunching() {
    auto director = Director::getInstance();
    auto view = director->getOpenGLView();
    if (!view) {
        view = GLViewImpl::createWithRect("ASTER - Turn Battle", Rect(0, 0, 1152, 648));
        director->setOpenGLView(view);
    }
    view->setDesignResolutionSize(1280, 720, ResolutionPolicy::SHOW_ALL);
    director->setAnimationInterval(1.0 / 60.0);
    director->setDisplayStats(false);
    director->getEventDispatcher()->addCustomEventListener(Director::EVENT_RESET,
                                                           [](EventCustom*) { ReferenceAudio::shutdown(); });
    FileUtils::getInstance()->addSearchPath("Resources");
    if (const char* smokeSave = std::getenv("ASTER_SMOKE_SAVE_DIR")) {
        std::string path = std::string(smokeSave) + "/";
        FileUtils::getInstance()->createDirectory(path);
        FileUtils::getInstance()->setWritablePath(path);
    }
    const char* captureDirectory = std::getenv("ASTER_GALLERY_CAPTURE_DIR");
    const char* smokeMode = std::getenv("ASTER_SMOKE_SCENE");
    if (smokeMode && std::getenv("ASTER_CUTSCENES_OFF"))
        UserDefault::getInstance()->setBoolForKey("cutscenes", false);
#if CC_TARGET_PLATFORM == CC_PLATFORM_WIN32
    if (smokeMode || captureDirectory)
        glfwHideWindow(static_cast<GLViewImpl*>(view)->getWindow());
#endif
    Scene* scene =
        (captureDirectory || std::getenv("ASTER_OPEN_GALLERY"))
            ? static_cast<Scene*>(ResourceGallery::create())
        : smokeMode && std::string(smokeMode) == "battle" ? static_cast<Scene*>(GameScene::create())
        : smokeMode && std::string(smokeMode) == "audio"
            ? static_cast<Scene*>(AudioLibraryScene::create([] {}))
        : smokeMode && std::string(smokeMode) == "skill" ? Scene::create()
                                                         : static_cast<Scene*>(FrontendScene::create());
    if (!scene)
        return false;
    director->runWithScene(scene);
    if (smokeMode && std::string(smokeMode) == "battle" && !std::getenv("ASTER_MANUAL_BATTLE") &&
        !std::getenv("ASTER_BASIC_BATTLE"))
        static_cast<GameScene*>(scene)->setAutoBattle(true);
    if (smokeMode && std::getenv("ASTER_CAPTURE_DIR")) {
        const std::string directory = std::getenv("ASTER_CAPTURE_DIR");
        const std::string mode = smokeMode;
        SkillPresentation* skill = nullptr;
        auto counters = std::make_shared<std::array<int, 2>>();
        if (mode == "battle" && std::getenv("ASTER_BASIC_BATTLE")) {
            auto tap = [scene](Vec2 point) {
                auto d = Director::getInstance();
                auto v = d->getOpenGLView();
                auto ui = d->convertToUI(point);
                auto viewport = v->getViewPortRect();
                float x = ui.x * v->getScaleX() + viewport.origin.x;
                float y = ui.y * v->getScaleY() + viewport.origin.y;
                intptr_t finger = 992;
                v->handleTouchesBegin(1, &finger, &x, &y);
                v->handleTouchesEnd(1, &finger, &x, &y);
            };
            scene->schedule(
                [scene, tap](float) {
                    const auto state = static_cast<GameScene*>(scene)->presentationDiagnostic();
                    if (state.find("manual=1") == std::string::npos ||
                        state.find("presenting=1") != std::string::npos)
                        return;
                    const std::array<Vec2, 4> targets{{{850, 360}, {980, 425}, {980, 290}, {1090, 360}}};
                    for (int i = 0; i < 4; ++i) {
                        auto at = state.find("unit" + std::to_string(i + 4) + " hp=");
                        if (at != std::string::npos && std::stoi(state.substr(at + 9)) > 0) {
                            tap(Vec2(1023, 57));
                            tap(targets[i]);
                            break;
                        }
                    }
                },
                .35f, "basic-attack-smoke-input");
            if (std::getenv("ASTER_PAUSE_SKILL")) {
                scene->scheduleOnce([tap](float) { tap(Vec2(1233, 667)); }, 1.f, "basic-open-menu");
                scene->scheduleOnce(
                    [scene, directory](float) {
                        std::ofstream(directory + "/pause-before.txt")
                            << static_cast<GameScene*>(scene)->presentationDiagnostic();
                    },
                    1.15f, "basic-pause");
                scene->scheduleOnce(
                    [scene, directory, tap](float) {
                        std::ofstream(directory + "/pause-after.txt")
                            << static_cast<GameScene*>(scene)->presentationDiagnostic();
                        tap(Vec2(640, 415));
                    },
                    2.2f, "basic-resume");
            }
            if (std::getenv("ASTER_FAST_BATTLE"))
                scene->scheduleOnce([tap](float) { tap(Vec2(1170, 667)); }, .1f, "basic-2x");
        }
        if (mode == "battle" && std::getenv("ASTER_INPUT_BATTLE")) {
            // Drive the real Cocos touch dispatcher only in the isolated renderer smoke.
            auto tap = [scene, directory](Vec2 point, const std::string& name) {
                auto d = Director::getInstance();
                auto v = d->getOpenGLView();
                auto ui = d->convertToUI(point);
                auto viewport = v->getViewPortRect();
                float x = ui.x * v->getScaleX() + viewport.origin.x;
                float y = ui.y * v->getScaleY() + viewport.origin.y;
                intptr_t finger = 991;
                v->handleTouchesBegin(1, &finger, &x, &y);
                v->handleTouchesEnd(1, &finger, &x, &y);
                std::ofstream(directory + "/input-" + name + ".txt")
                    << static_cast<GameScene*>(scene)->presentationDiagnostic();
            };
            scene->scheduleOnce([tap](float) { tap(Vec2(1023, 57), "select-s1"); }, 2.f, "input-s1");
            scene->scheduleOnce([tap](float) { tap(Vec2(190, 360), "reject-ally"); }, 3.f, "input-invalid");
            scene->scheduleOnce([tap](float) { tap(Vec2(850, 360), "enemy-target"); }, 4.f, "input-target");
            scene->scheduleOnce([tap](float) { tap(Vec2(1221, 57), "clarissa-s3"); }, 7.5f, "input-clarissa");
            scene->scheduleOnce([tap](float) { tap(Vec2(1221, 57), "aramintha-s3"); }, 14.f,
                                "input-aramintha");
            scene->scheduleOnce([tap](float) { tap(Vec2(1221, 57), "achates-s3"); }, 23.f, "input-achates");
        }
        if (mode == "battle" && std::getenv("ASTER_SKIP_SKILL")) {
            for (float time : {3.f, 7.f, 11.f})
                scene->scheduleOnce(
                    [scene](float) { static_cast<GameScene*>(scene)->skipSkillPresentation(); }, time,
                    "battle-smoke-skip-" + std::to_string(time));
        }
        if (mode == "battle" && std::getenv("ASTER_PAUSE_SKILL") && !std::getenv("ASTER_BASIC_BATTLE")) {
            scene->scheduleOnce(
                [scene, directory](float) {
                    EventKeyboard pause(EventKeyboard::KeyCode::KEY_SPACE, true);
                    scene->getEventDispatcher()->dispatchEvent(&pause);
                    std::ofstream(directory + "/pause-before.txt")
                        << static_cast<GameScene*>(scene)->presentationDiagnostic();
                },
                .9f, "battle-smoke-pause");
            scene->scheduleOnce(
                [scene, directory](float) {
                    std::ofstream(directory + "/pause-after.txt")
                        << static_cast<GameScene*>(scene)->presentationDiagnostic();
                    EventKeyboard skip(EventKeyboard::KeyCode::KEY_ESCAPE, false);
                    scene->getEventDispatcher()->dispatchEvent(&skip);
                },
                2.1f, "battle-smoke-paused-escape");
        }
        if (mode == "audio" && !ReferenceAudio::catalog().empty())
            ReferenceAudio::playTrack(ReferenceAudio::catalog().front().id);
        if (mode == "skill") {
            std::string error;
            auto id = std::getenv("ASTER_SKILL_ID");
            SkillPresentation::Callbacks callbacks;
            callbacks.onHit = [counters] { ++(*counters)[0]; };
            callbacks.onComplete = [counters](bool) {
                ++(*counters)[1];
                ReferenceAudio::stopEffects();
            };
            skill = SkillPresentation::create(id ? id : "c1017.s3", callbacks, error);
            if (skill) {
                scene->addChild(skill);
                ReferenceAudio::playSkill(id ? std::string(id).substr(0, 5) : "c1017", 3);
            } else {
                std::ofstream(directory + "/error.txt") << error;
                director->end();
            }
            if (skill && std::getenv("ASTER_SKIP_SKILL"))
                scene->scheduleOnce([skill](float) { skill->skip(); }, 1.f, "smoke-skip");
            if (skill && std::getenv("ASTER_PAUSE_SKILL")) {
                scene->scheduleOnce(
                    [skill, directory](float) {
                        skill->setPaused(true);
                        ReferenceAudio::setPaused(true);
                        std::ofstream(directory + "/pause-before.txt") << skill->diagnostic() << "\n"
                                                                       << ReferenceAudio::diagnostic();
                    },
                    .9f, "smoke-pause");
                scene->scheduleOnce(
                    [skill, directory](float) {
                        std::ofstream(directory + "/pause-after.txt") << skill->diagnostic() << "\n"
                                                                      << ReferenceAudio::diagnostic();
                        skill->setPaused(false);
                        ReferenceAudio::setPaused(false);
                    },
                    2.1f, "smoke-resume");
            }
        }
        const std::vector<float> moments =
            mode == "battle" && std::getenv("ASTER_BASIC_BATTLE")
                ? std::vector<float>{.6f,   1.2f,  1.65f, 2.1f,  3.4f, 4.6f, 5.8f, 7.5f, 9.5f,
                                     11.8f, 13.4f, 15.f,  16.6f, 19.f, 22.f, 26.f, 32.f}
            : mode == "battle" ? std::vector<float>{.6f, 3.f, 7.f, 11.f, 16.f, 23.f, 52.f}
            : mode == "skill"  ? std::vector<float>{.4f,  1.5f, 2.5f,
                                                    3.8f, 5.2f, std::getenv("ASTER_PAUSE_SKILL") ? 8.5f : 7.f}
                               : std::vector<float>{.6f, 1.8f};
        struct CaptureState {
            float time = 0;
            size_t frame = 0;
            bool pending = false;
        };
        auto capture = std::make_shared<CaptureState>();
        scene->schedule(
            [scene, directory, mode, skill, counters, moments, capture](float delta) {
                capture->time += delta;
                if (capture->pending || capture->frame >= moments.size() ||
                    capture->time < moments[capture->frame])
                    return;
                const size_t frame = capture->frame++;
                capture->pending = true;
                auto stem = directory + "/frame-" + std::to_string(frame + 1);
                std::ofstream log(stem + ".txt");
                if (skill)
                    log << skill->diagnostic() << "\nhit_callbacks=" << (*counters)[0]
                        << " complete_callbacks=" << (*counters)[1];
                else if (auto battle = dynamic_cast<GameScene*>(scene))
                    log << battle->presentationDiagnostic();
                else if (auto frontend = dynamic_cast<FrontendScene*>(scene))
                    log << frontend->diagnostic();
                log << "\n" << ReferenceAudio::diagnostic();
                utils::captureScreen(
                    [capture, frame, moments](bool success, const std::string&) {
                        capture->pending = false;
                        if (frame + 1 == moments.size())
                            Director::getInstance()->end();
                    },
                    stem + ".png");
            },
            .05f, "presentation-smoke-capture");
    }
    // Opt-in renderer smoke check; normal launches always start at the lobby.
    if (captureDirectory) {
        const std::string directory(captureDirectory);
        auto gallery = static_cast<ResourceGallery*>(scene);
        for (int frame = 1; frame <= 2; ++frame) {
            gallery->scheduleOnce(
                [gallery, directory, frame](float) {
                    const auto stem = directory + "/character-frame-" + std::to_string(frame);
                    std::ofstream log(stem + ".txt");
                    log << (gallery->spinePreview()      ? gallery->spinePreview()->diagnostic()
                            : gallery->cutscenePreview() ? gallery->cutscenePreview()->diagnostic()
                                                         : "NO ANIMATION PREVIEW");
                    utils::captureScreen(
                        [frame](bool success, const std::string&) {
                            if (frame == 2)
                                Director::getInstance()->end();
                        },
                        stem + ".png");
                },
                frame == 1 ? .4f : 1.6f, "gallery-smoke-" + std::to_string(frame));
        }
    }
    return true;
}
void AppDelegate::applicationDidEnterBackground() {
    if (std::getenv("ASTER_SMOKE_SCENE")) {
        if (const char* directory = std::getenv("ASTER_CAPTURE_DIR"))
            std::ofstream(std::string(directory) + "/lifecycle.txt", std::ios::app)
                << "smoke background: continue deterministic renderer validation\n";
        return;
    }
    ReferenceAudio::setPaused(true);
    auto director = Director::getInstance();
    if (auto scene = dynamic_cast<GameScene*>(director->getRunningScene()))
        scene->suspend();
    director->stopAnimation();
}
void AppDelegate::applicationWillEnterForeground() {
    auto director = Director::getInstance();
    director->startAnimation();
    ReferenceAudio::setPaused(false);
    if (auto scene = dynamic_cast<GameScene*>(director->getRunningScene()))
        scene->resumeGame();
}
