-- Behaviour decisions live here; PatrolMotor executes Unity movement and animation.
local M = {}

-- Constructors must not modify Unity objects: reload stages replacements first.
function M.new(motor, savedState)
    assert(motor.PointCount >= 2, "PatrolGuard needs at least two patrol points")
    local targetIndex = savedState and savedState.targetIndex or 0
    targetIndex = targetIndex % motor.PointCount
    local waitRemaining = savedState and savedState.waitRemaining or 0
    local settings = {
        speed = 1.5, chaseSpeed = 2.5,
        arrivalDistance = 0.15, waitSeconds = 0.8,
        detectDistance = 4, loseDistance = 6,
        stopDistance = 1.2, resumeDistance = 1.5
    }
    -- Validate hysteresis thresholds before creating replacement behaviour.
    assert(settings.loseDistance > settings.detectDistance,
        "loseDistance must exceed detectDistance")
    assert(settings.resumeDistance > settings.stopDistance,
        "resumeDistance must exceed stopDistance")
    local pursuing = savedState and savedState.pursuing or false
    local holding = savedState and savedState.holding or false
    local returning = savedState and savedState.returning or false

    return {
        save_state = function()
            return {
                targetIndex = targetIndex, waitRemaining = waitRemaining,
                pursuing = pursuing, holding = holding, returning = returning
            }
        end,
        update = function(dt)
            -- Different enter/exit thresholds prevent switching at the boundary.
            local distance = motor.TargetDistance
            if pursuing and (not motor.HasTarget or distance > settings.loseDistance) then
                pursuing = false
                holding = false
                returning = true
                waitRemaining = 0
            elseif not pursuing and motor.HasTarget and distance < settings.detectDistance then
                pursuing = true
                returning = false
                waitRemaining = 0
            end

            if pursuing then
                if holding and distance > settings.resumeDistance then
                    holding = false
                elseif not holding and distance <= settings.stopDistance then
                    holding = true
                end
                if holding then
                    motor:SetState("hold")
                    motor:Idle(dt)
                else
                    motor:SetState("chase")
                    motor:SetSpeed(settings.chaseSpeed)
                    motor:MoveTo(motor:GetTargetPosition(), dt)
                end
                return
            end

            motor:SetSpeed(settings.speed)
            if waitRemaining > 0 then
                waitRemaining = math.max(0, waitRemaining - dt)
                motor:SetState("wait")
                motor:Idle(dt)
                return
            end

            motor:SetState(returning and "return" or "patrol")
            local target = motor:GetPatrolPoint(targetIndex)
            if motor:DistanceTo(target) <= settings.arrivalDistance then
                motor:NotifyArrival()
                returning = false
                targetIndex = (targetIndex + 1) % motor.PointCount
                waitRemaining = settings.waitSeconds
                motor:Idle(dt)
            else
                motor:MoveTo(target, dt)
            end
        end,
        shutdown = function()
            motor:SetState("stopped")
            motor:Stop()
        end
    }
end

return M
