using System;
using UnityEngine;
using Singularity.Game;

/// <summary>
/// THE PHONE IS MOVING AND THE SKY IS NOT.
///
/// Tilt reads the device and hands the sky a lean. Every part of the chain around
/// it was checked — SkyChecks proves the field's brightness, its star size and how
/// far it travels at each motion setting — and NOTHING checked the part that reads
/// the device, because `Input.gyro` returned null in the stubs and
/// `SystemInfo.supportsGyroscope` returned a constant false. Tilt's body had never
/// been executed by anything. The stubs answer properly now; this plays a phone.
///
/// THE BUG IT FOUND is the one a real device hits and an emulator does not.
/// `Input.gyro.gravity` is a FUSED sensor: Android synthesises it from the gyro
/// and the accelerometer, and on a good many handsets that report
/// supportsGyroscope == true it answers a flat zero — the hardware is there, the
/// fusion is not. Tilt latched `_gyro = true` once at startup and never looked
/// again, so those devices took the dead branch on every frame for the whole
/// session: Live false, Look zero, the sky nailed to the middle of the screen,
/// with a perfectly good accelerometer sitting next to it that was never asked.
///
/// A ZERO IS UNAMBIGUOUS HERE, which is what makes the fallback safe. Gravity has
/// a magnitude of one whichever way a phone is held — face up, edge on, upside
/// down — so there is no pose that reads zero. A zero is the channel declining to
/// answer, and the only question left is whether the OTHER channel will.
/// </summary>
static class TiltChecks
{
    /// <summary>Hand the device a pose and let Tilt read it for a while.</summary>
    static Vector2 Hold(Vector3 gyroGravity, Vector3 accel, float seconds, float dt = 1f / 60f)
    {
        Input.gyro.gravity = gyroGravity;
        Input.acceleration = accel;
        for (float t = 0; t < seconds; t += dt) Tilt.Sample(dt);
        return Tilt.Look;
    }

    /// <summary>Upright, then rolled left by an eighth of a turn.</summary>
    static readonly Vector3 Upright = new Vector3(0f, -1f, 0f);
    static readonly Vector3 Rolled = new Vector3(-0.38f, -0.92f, 0f);

    public static void Run(Action<bool, string> ok)
    {
        // ---- a handset whose fused gravity channel works ---------------------
        SystemInfo.supportsGyroscope = true;
        Tilt.Recentred();
        Hold(Upright, Upright, 1.0f);
        Vector2 good = Hold(Rolled, Rolled, 0.35f);

        ok(good.magnitude > 0.2f,
           "a phone with a working gravity sensor leans " + good.magnitude.ToString("0.000")
           + " and the sky needs something to parallax on");
        ok(Tilt.Live, "Tilt reports no sensor on a device that is answering");

        // ---- AND ONE WHOSE GYRO IS PRESENT BUT ANSWERS NOTHING ---------------
        //
        // supportsGyroscope true, gravity flat zero, accelerometer fine. This is
        // the case that shipped broken.
        SystemInfo.supportsGyroscope = true;
        Tilt.Recentred();
        Hold(Vector3.zero, Upright, 1.0f);
        Vector2 fallback = Hold(Vector3.zero, Rolled, 0.35f);

        ok(fallback.magnitude > 0.2f,
           "a phone whose fused gravity channel answers zero leans "
           + fallback.magnitude.ToString("0.000")
           + " — it never falls back to the accelerometer, so the sky is nailed down");
        ok(Tilt.Live, "Tilt calls a device dead while its accelerometer is answering");

        // ---- and a device with nothing at all is still a shrug ---------------
        SystemInfo.supportsGyroscope = false;
        Tilt.Recentred();
        Hold(Vector3.zero, Vector3.zero, 1.0f);
        ok(!Tilt.Live, "Tilt claims a sensor on a device that answers nothing");
        ok(Tilt.Look.magnitude < 0.001f, "the sky drifts on a device with no sensors");

        Console.WriteLine("tilt: a rolled phone leans " + good.magnitude.ToString("0.00")
                        + " on fused gravity and " + fallback.magnitude.ToString("0.00")
                        + " when that channel is dead and only the accelerometer answers");

        // ---- the centre follows, so a held pose stops being a lean -----------
        SystemInfo.supportsGyroscope = true;
        Tilt.Recentred();
        Hold(Upright, Upright, 1.0f);
        Hold(Rolled, Rolled, 0.35f);
        Vector2 settled = Hold(Rolled, Rolled, 14f);
        ok(settled.magnitude < good.magnitude * 0.5f,
           "a pose held for fourteen seconds is still reading as a lean ("
           + settled.magnitude.ToString("0.000") + ")");
        Console.WriteLine("tilt: hold that roll and the horizon follows it back to "
                        + settled.magnitude.ToString("0.00") + " — the centre is where you are looking");

        SystemInfo.supportsGyroscope = false;
        Input.gyro.gravity = Vector3.zero;
        Input.acceleration = Vector3.zero;
        Tilt.Recentred();
    }
}
