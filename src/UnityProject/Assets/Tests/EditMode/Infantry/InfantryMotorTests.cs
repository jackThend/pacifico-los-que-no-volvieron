using System;
using NUnit.Framework;
using Pacifico.Core.Common;
using Pacifico.Core.Infantry;

namespace Pacifico.Tests.Infantry
{
    /// <summary>ROADMAP 3.1 — movimiento ágil, deslizamiento táctico y ausencia de tirones a cualquier tasa de fotogramas.</summary>
    public class InfantryMotorTests
    {
        /// <summary>Simula sobre suelo plano infinito: integra la posición como haría el CharacterController.</summary>
        private sealed class Sim
        {
            public readonly InfantryMotor Motor = new InfantryMotor();
            public Vec3 Position;
            public bool Grounded = true;

            public void Run(InfantryInput input, float seconds, float dt, Action<InfantryMotor> perFrame = null)
            {
                int steps = (int)Math.Round(seconds / dt);
                for (int i = 0; i < steps; i++) Frame(input, dt, perFrame);
            }

            public void Frame(InfantryInput input, float dt, Action<InfantryMotor> perFrame = null)
            {
                Vec3 d = Motor.Step(input, dt, Grounded);
                Position += d;
                if (Position.Y <= 0f)
                {
                    Position = new Vec3(Position.X, 0f, Position.Z);
                    Grounded = true;
                }
                else
                {
                    Grounded = false;
                }
                perFrame?.Invoke(Motor);
            }
        }

        private static InfantryInput Forward(bool sprint = false) => new InfantryInput { MoveZ = 1f, Sprint = sprint };

        [Test]
        public void Ajustes_PorDefectoSonCoherentes()
        {
            var r = new InfantryMovementSettings().Validate();
            Assert.That(r.IsValid, Is.True, r.ToString());
        }

        [Test]
        public void Caminar_AlcanzaLaVelocidadRapido_SinSobrepasarla()
        {
            var sim = new Sim();
            float max = 0f;
            sim.Run(Forward(), 1f, 1f / 60f, m => max = Math.Max(max, m.HorizontalSpeed));
            float walk = sim.Motor.Settings.WalkSpeed;
            Assert.That(sim.Motor.HorizontalSpeed, Is.EqualTo(walk).Within(1e-3f), "respuesta inmediata (GDD §3.1)");
            Assert.That(max, Is.LessThanOrEqualTo(walk + 1e-4f), "sin sobreimpulso: la velocidad nunca rebasa el objetivo");
        }

        [Test]
        public void Jerarquia_DeVelocidades()
        {
            float SpeedWith(InfantryInput input)
            {
                var sim = new Sim();
                sim.Run(input, 1.5f, 1f / 60f);
                return sim.Motor.HorizontalSpeed;
            }

            float crouch = SpeedWith(new InfantryInput { MoveZ = 1f, Crouch = true });
            float walk = SpeedWith(Forward());
            float sprint = SpeedWith(Forward(sprint: true));
            float aim = SpeedWith(new InfantryInput { MoveZ = 1f, Aim = true });
            float back = SpeedWith(new InfantryInput { MoveZ = -1f });

            Assert.That(crouch, Is.LessThan(walk));
            Assert.That(walk, Is.LessThan(sprint));
            Assert.That(aim, Is.LessThan(walk), "apuntar con las miras ralentiza");
            Assert.That(back, Is.LessThan(walk), "retroceder es más lento");
        }

        [Test]
        public void MovimientoDiagonal_NoEsMasRapido()
        {
            var sim = new Sim();
            sim.Run(new InfantryInput { MoveX = 1f, MoveZ = 1f }, 2f, 1f / 60f);
            Assert.That(sim.Motor.HorizontalSpeed, Is.LessThanOrEqualTo(sim.Motor.Settings.WalkSpeed + 1e-4f));
        }

        [Test]
        public void Direccion_SigueALaMirada()
        {
            var sim = new Sim();
            sim.Run(new InfantryInput { MoveZ = 1f, YawDeg = 90f }, 2f, 1f / 60f);
            Assert.That(sim.Position.X, Is.GreaterThan(3f), "mirando al este, W avanza hacia +X");
            Assert.That(Math.Abs(sim.Position.Z), Is.LessThan(1e-3f));
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 144f)]
        public void Trayectoria_IgualA30y144FPSQueA60(float dt)
        {
            var reference = new Sim();
            var other = new Sim();
            // Arranca, corre, gira y frena: cubre aceleración, carrera y deceleración.
            foreach (var sim in new[] { reference, other })
            {
                float step = sim == reference ? 1f / 60f : dt;
                sim.Run(Forward(sprint: true), 2f, step);
                sim.Run(new InfantryInput { MoveZ = 1f, MoveX = 1f, YawDeg = 30f }, 1f, step);
                sim.Run(default, 1f, step);
            }
            float distance = (reference.Position - other.Position).Magnitude;
            Assert.That(distance, Is.LessThan(0.08f), "menos de 8 cm tras 4 s y 15 m recorridos");
        }

        [Test]
        public void PasosIrregulares_NoProducenSaltosNiOscilaciones()
        {
            var rng = new Random(60);
            var sim = new Sim();
            float previousSpeed = 0f;
            bool decreasedWhileAccelerating = false;
            for (int i = 0; i < 600; i++)
            {
                float dt = (float)(1.0 / 200.0 + rng.NextDouble() * (1.0 / 30.0 - 1.0 / 200.0));
                sim.Frame(Forward(sprint: true), dt);
                if (sim.Motor.HorizontalSpeed + 1e-4f < previousSpeed && sim.Motor.IsSprinting) decreasedWhileAccelerating = true;
                previousSpeed = sim.Motor.HorizontalSpeed;
                Assert.That(sim.Motor.HorizontalSpeed, Is.LessThanOrEqualTo(sim.Motor.Settings.SprintSpeed + 1e-4f));
            }
            Assert.That(decreasedWhileAccelerating, Is.False, "la velocidad crece monótonamente hasta el objetivo");
        }

        [Test]
        public void Salto_AlcanzaLaAlturaConfiguradaEnCualquierTasa()
        {
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f })
            {
                var sim = new Sim();
                float apex = 0f;
                sim.Frame(new InfantryInput { Jump = true }, dt);
                sim.Run(default, 1.5f, dt, _ => apex = Math.Max(apex, sim.Position.Y));
                Assert.That(apex, Is.EqualTo(sim.Motor.Settings.JumpHeight).Within(0.02f), "dt = " + dt);
                Assert.That(sim.Grounded, Is.True);
                Assert.That(sim.Motor.LandingCount, Is.EqualTo(1));
                Assert.That(sim.Motor.LastLandingSpeed, Is.LessThan(-2f));
            }
        }

        [Test]
        public void MantenerSalto_NoSaltaRepetidamente()
        {
            var sim = new Sim();
            sim.Run(new InfantryInput { Jump = true }, 3f, 1f / 60f);
            Assert.That(sim.Motor.LandingCount, Is.EqualTo(1), "hay que soltar y volver a pulsar");
        }

        [Test]
        public void Deslizamiento_DesdeLaCarrera_EsBreveYTermina()
        {
            var sim = new Sim();
            sim.Run(Forward(sprint: true), 1.5f, 1f / 60f);
            float stamina = sim.Motor.Stamina;

            var slideInput = new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true };
            sim.Frame(slideInput, 1f / 60f);
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Sliding));
            Assert.That(sim.Motor.HorizontalSpeed, Is.GreaterThanOrEqualTo(sim.Motor.Settings.SlideStartSpeed - 0.2f));
            Assert.That(sim.Motor.Stamina, Is.LessThan(stamina - sim.Motor.Settings.SlideStaminaCost + 0.01f));

            sim.Run(slideInput, sim.Motor.Settings.SlideMaxDuration + 0.1f, 1f / 60f);
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Crouching), "al terminar, agachado si sigue pulsado");
        }

        [Test]
        public void Deslizamiento_SoloSeIniciaCorriendo_YTieneEnfriamiento()
        {
            var walking = new Sim();
            walking.Run(Forward(), 1f, 1f / 60f);
            walking.Frame(new InfantryInput { MoveZ = 1f, Crouch = true }, 1f / 60f);
            Assert.That(walking.Motor.Stance, Is.EqualTo(Stance.Crouching), "caminando, C solo agacha");

            var sim = new Sim();
            sim.Run(Forward(sprint: true), 1.5f, 1f / 60f);
            sim.Frame(new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true }, 1f / 60f);
            sim.Run(Forward(sprint: true), sim.Motor.Settings.SlideMaxDuration + 0.05f, 1f / 60f); // termina y se levanta
            sim.Frame(new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true }, 1f / 60f);
            Assert.That(sim.Motor.Stance, Is.Not.EqualTo(Stance.Sliding), "enfriamiento entre deslizamientos");
        }

        [Test]
        public void SinAlturaLibre_SigueAgachado()
        {
            var sim = new Sim();
            sim.Motor.Step(new InfantryInput { Crouch = true }, 1f / 60f, true);
            sim.Motor.Step(new InfantryInput(), 1f / 60f, true, canStand: false);
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Crouching));
            sim.Motor.Step(new InfantryInput(), 1f / 60f, true, canStand: true);
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Standing));
        }

        [Test]
        public void CambioDePostura_EsGradual()
        {
            var motor = new InfantryMotor();
            motor.Step(new InfantryInput { Crouch = true }, 1f / 60f, true);
            Assert.That(motor.Height, Is.LessThan(motor.Settings.StandingHeight));
            Assert.That(motor.Height, Is.GreaterThan(motor.Settings.CrouchHeight), "sin salto de altura en un fotograma");
            for (int i = 0; i < 60; i++) motor.Step(new InfantryInput { Crouch = true }, 1f / 60f, true);
            Assert.That(motor.Height, Is.EqualTo(motor.Settings.CrouchHeight));
        }

        [Test]
        public void Resistencia_SeAgotaCorriendo_YExigeRecuperarse()
        {
            var sim = new Sim();
            sim.Run(Forward(sprint: true), 12f, 1f / 60f);
            Assert.That(sim.Motor.IsExhausted, Is.True);
            Assert.That(sim.Motor.IsSprinting, Is.False, "agotado no puede correr");
            Assert.That(sim.Motor.HorizontalSpeed, Is.EqualTo(sim.Motor.Settings.WalkSpeed).Within(0.05f));

            sim.Run(default, 4f, 1f / 60f);
            Assert.That(sim.Motor.IsExhausted, Is.False);
        }

        [Test]
        public void CuestaAbajo_ElEmpujeAlSueloSigueUnaPendienteDe45Grados()
        {
            // Corriendo a 5,4 m/s, bajar una pendiente de 35° exige descender ≥ 3,8 m/s: con solo 2 m/s el
            // CharacterController se despegaría del suelo cada fotograma (isGrounded intermitente = tirones).
            var motor = new InfantryMotor();
            Vec3 d = Vec3.Zero;
            for (int i = 0; i < 120; i++) d = motor.Step(Forward(sprint: true), 1f / 60f, grounded: true);
            float horizontal = d.HorizontalMagnitude;
            Assert.That(-d.Y, Is.GreaterThanOrEqualTo(horizontal * (float)Math.Tan(45.0 * Math.PI / 180.0) - 1e-5f));
        }

        [Test]
        public void AlSalirDeUnBorde_EmpiezaACaerDesdeVelocidadVerticalNula()
        {
            var motor = new InfantryMotor();
            for (int i = 0; i < 60; i++) motor.Step(Forward(sprint: true), 1f / 60f, grounded: true);
            motor.Step(Forward(sprint: true), 1f / 60f, grounded: false);
            Assert.That(motor.Velocity.Y, Is.EqualTo(-motor.Settings.Gravity / 60f).Within(1e-3f),
                "sin arrastrar el empuje de pegado al suelo (-7 m/s) a la caída");
        }

        [Test]
        public void SaltarDesdeElDeslizamiento_ConAgacharPulsado_Salta()
        {
            var sim = new Sim();
            sim.Run(Forward(sprint: true), 1.5f, 1f / 60f);
            sim.Frame(new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true }, 1f / 60f);
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Sliding));

            sim.Frame(new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true, Jump = true }, 1f / 60f);
            Assert.That(sim.Motor.Velocity.Y, Is.GreaterThan(0f), "el salto no se pierde");
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Standing));
            Assert.That(sim.Motor.HorizontalSpeed, Is.GreaterThan(sim.Motor.Settings.SprintSpeed), "conserva el impulso");
        }

        [Test]
        public void DeslizamientoContraUnaPared_ResbalaALoLargoDeElla()
        {
            var sim = new Sim();
            // Carrera en diagonal (45°) hacia una pared perpendicular a Z.
            sim.Run(new InfantryInput { MoveZ = 1f, Sprint = true, YawDeg = 45f }, 1.5f, 1f / 60f);
            sim.Frame(new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true, YawDeg = 45f }, 1f / 60f);
            Assert.That(sim.Motor.Stance, Is.EqualTo(Stance.Sliding));

            sim.Motor.OnWallHit(new Vec3(0f, 0f, -1f));
            float alongWall = sim.Motor.Velocity.X;
            // Mirando a lo largo del muro (+X). Si se mirase contra él, el leve gobierno del deslizamiento volvería a
            // orientarlo hacia la pared y el siguiente contacto del CharacterController lo recortaría.
            sim.Frame(new InfantryInput { MoveZ = 1f, Sprint = true, Crouch = true, YawDeg = 90f }, 1f / 60f);
            Assert.That(sim.Motor.Velocity.Z, Is.EqualTo(0f).Within(0.05f), "no vuelve a empujar contra la pared");
            float frictionLoss = sim.Motor.Settings.SlideFriction / 60f;
            Assert.That(sim.Motor.Velocity.X, Is.GreaterThan(alongWall - frictionLoss - 0.05f), "solo pierde lo que quita el rozamiento");
        }

        [Test]
        public void ChoqueConPared_AnulaLaVelocidadContraElla()
        {
            var sim = new Sim();
            sim.Run(Forward(), 1f, 1f / 60f);
            sim.Motor.OnWallHit(new Vec3(0f, 0f, -1f));
            Assert.That(sim.Motor.Velocity.Z, Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
