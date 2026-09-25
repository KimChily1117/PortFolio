-- Add KimchilyLuaBehaviour, assign this script, and optionally add a reference named lamp.
function on_start()
    log("Lua world started")
    start(function()
        while true do
            self.set_scale(1.15, 1.15, 1.15)
            if refs.lamp then refs.lamp.set_active(true) end
            wait_seconds(0.5)
            self.set_scale(1, 1, 1)
            if refs.lamp then refs.lamp.set_active(false) end
            wait_seconds(0.5)
        end
    end)
end

function on_update(dt)
    self.rotate(0, 45 * dt, 0)
end

function on_disable()
    log("Lua routines cancelled")
end
