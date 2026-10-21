-- Host-only callbacks. Inputs are plain numbers from a server checkpoint.
-- C# independently checks the same published selection contract and all damage.
function on_pattern_requested(ctx)
    local pattern
    if ctx.ordinal % 4 == 0 then
        pattern = 6 -- mandatory Dawn meteor: impact followed by jumpable shockwave
    elseif ctx.ordinal % 4 == 2 then
        pattern = 8 -- locked-direction charge, with server-owned warning and recovery
    else
        local pool = {0, 2, 3, 5}
        if ctx.phase == 2 then pool = {0, 1, 2, 4, 5} end
        local index = ctx.roll % #pool + 1
        pattern = pool[index]
        if pattern == ctx.previous then pattern = pool[index % #pool + 1] end
    end
    emit_pattern(pattern, ctx.start_tick)
    -- Bounded authoring primitives: approach, preparation, then the authored attack sequence.
    -- Damage, range, movement speed and legal target order remain C# server rules.
    local count = ctx.player_count
    local target = ctx.ordinal % count
    local prepare_ticks = 36
    emit_action(0, 0, target)
    if pattern == 6 then
        for wave = 0, 3 do
            emit_action(2, prepare_ticks + wave * 87, (target + wave) % count)
        end
    elseif pattern == 8 then
        for charge = 0, 2 do
            emit_action(4, prepare_ticks + charge * 162, (target + charge) % count)
        end
    else
        emit_action(1, prepare_ticks, target)
    end
end

function on_pattern_confirmed(ctx)
    -- Presentation/diagnostics callback. It cannot emit a new attack.
    return ctx.pattern
end

-- Independent room hazard schedule; never waits for boss stagger or phase recovery.
function on_room_mark_requested(ctx)
    emit_pattern(7, ctx.start_tick)
        local marked = 0
        for pass = 0, 1 do
            for offset = 0, 3 do
                local slot = (ctx.roll + offset) % 4
                local bit = 2 ^ slot
                local alive = ctx.alive_mask % (bit * 2) >= bit
                local previous = ctx.previous_mark_mask % (bit * 2) >= bit
                if marked < 2 and alive and ((pass == 0 and not previous) or (pass == 1 and previous)) then
                    emit_action(3, 0, slot)
                    marked = marked + 1
                end
            end
        end
end
