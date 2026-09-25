-- Change this file, test with Play, and publish a new QR. No APK rebuild is needed.
local speed = 100
local elapsed = 0

function on_start()
    log("Published Lua world started: revision 2")
    start(function()
        while true do
            refs.beacon.set_active(false)
            wait_seconds(0.3)
            refs.beacon.set_active(true)
            wait_seconds(0.3)
        end
    end)
end

function on_update(dt)
    elapsed = elapsed + dt
    self.rotate(0, speed * dt, 0)
    self.set_position(0, math.sin(elapsed * 2) * 0.12, 0)
end
