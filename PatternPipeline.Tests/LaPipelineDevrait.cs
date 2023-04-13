using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace PatternPipeline.Tests
{
    public class LaPipelineDevrait
    {
        [Test]
        public void Executer_toutes_les_pipes_avec_le_meme_type_en_entree_et_en_sortie()
        {
            var pipe1 = CreerPipe("testInitiale", "test1");
            var pipe2 = CreerPipe("test1", "test2");
            var pipe3 = CreerPipe("test2", "testFinale");
            var pipeSource = CreerPipe(Arg.Any<string>(), "testInitiale");

            var resultat = new Pipeline<string>(Substitute.For<ILogger>())
                .Configurer(_ => _
                    .Ajouter(pipeSource)
                    .Ajouter(pipe1)
                    .Ajouter(pipe2)
                    .Ajouter(pipe3))
                .Executer();

            resultat.IsSuccess.Should().BeTrue();
            resultat.Value.Should().Be("testFinale");
            pipeSource.Received(1).Executer(Arg.Any<string>());
            pipe1.Received(1).Executer("testInitiale");
            pipe2.Received(1).Executer("test1");
            pipe3.Received(1).Executer("test2");
        }

        [Test]
        public void Executer_toutes_les_pipes_avec_des_types_différents()
        {
            var resultat = new Pipeline<IEnumerable<DateTime>>(Substitute.For<ILogger>())
                .Configurer(_ => _
                    .Ajouter(CreerPipe(Arg.Any<string>(), "test"))
                    .Ajouter(CreerPipe("test", true))
                    .Ajouter(CreerPipe(true, 1))
                    .Ajouter(CreerPipe(1, new[] { DateTime.MinValue }))
                ).Executer();

            resultat.IsSuccess.Should().BeTrue();
            resultat.Value.Should().BeEquivalentTo(new[] { DateTime.MinValue });
        }

        [Test]
        public void Executer_les_pipes_dans_lordre()
        {
            var pipe = CreerPipe("testInitiale", "test1");
            pipe.Executer("test1").Returns("test2");
            pipe.Executer("test2").Returns("testFinale");
            pipe.Executer(default).Returns("testInitiale");

            new Pipeline<string>(Substitute.For<ILogger>())
                .Configurer(_ => _
                    .Ajouter(pipe)
                    .Ajouter(pipe)
                    .Ajouter(pipe)
                    .Ajouter(pipe))
                .Executer();

            Received.InOrder(() =>
            {
                pipe.Executer(default);
                pipe.Executer("testInitiale");
                pipe.Executer("test1");
                pipe.Executer("test2");
            });
        }

        [Test]
        public void Renvoyer_une_erreur_lorsque_le_pipeline_nest_pas_complet()
        {
            var resultat = new Pipeline<IEnumerable<DateTime>>(Substitute.For<ILogger>())
                .Configurer(_ => _
                    .Ajouter(CreerPipe(Arg.Any<string>(), "test"))
                    .Ajouter(CreerPipe("test", true))
                    .Ajouter(CreerPipe(true, 1))
                ).Executer();
            resultat.IsFailed.Should().BeTrue();
            resultat.HasError(error => error.Message == "Le type de retour 'System.Int32' n'est pas valide.");
        }

        [Test]
        public void Continuer_lorsquUn_pipe_renvoie_une_exception_et_le_type_dEntree_est_egal_au_type_de_sortie()
        {
            var pipe1 = CreerPipe("testInitiale", "test1");
            pipe1.Executer(Arg.Any<string>()).Throws(new ArgumentNullException("exception pipe"));
            var pipe2 = CreerPipe("testInitiale", "test2");
            var pipe3 = CreerPipe("test2", "testFinale");
            var logger = Substitute.For<ILogger>();

            var resultat = new Pipeline<string>(logger)
                .Configurer(_ => _
                    .Ajouter(CreerPipe(Arg.Any<string>(), "testInitiale"))
                    .Ajouter(pipe1)
                    .Ajouter(pipe2)
                    .Ajouter(pipe3))
                .Executer();

            pipe2.Received(1).Executer("testInitiale");
            pipe3.Received(1).Executer("test2");
            resultat.Value.Should().Be("testFinale");
            logger.Received(1).LogError("Value cannot be null. (Parameter 'exception pipe')");
        }

        [Test]
        public void Renvoyer_une_erreur_lorsquUn_pipe_renvoie_une_exception_vers_un_pipe_de_type_différents()
        {
            var pipe = CreerPipe("test1", true);
            pipe.Executer(Arg.Any<string>()).Throws(
                new ArgumentNullException("exception pipe"));
            var logger = Substitute.For<ILogger>();

            var resultat = new Pipeline<bool>(logger)
                .Configurer(_ => _
                    .Ajouter(CreerPipe(Arg.Any<string>(), "test1"))
                    .Ajouter(pipe)
                    .Ajouter(CreerPipe(true, true)))
                .Executer();

            resultat.IsFailed.Should().BeTrue();
            resultat.Errors.Should().HaveCount(1);
            var erreurs = resultat.Errors.Select(_ => _.Message);
            erreurs.First().Should().Be("Value cannot be null. (Parameter 'exception pipe')");
        }

        [Test]
        public void Renvoyer_une_erreur_lorsquUne_sourceDePipeline_renvoie_une_exception()
        {
            var sourceDePipeline = CreerPipe(Arg.Any<string>(), "test1");
            sourceDePipeline.Executer(Arg.Any<string>()).Throws(
                new ArgumentNullException("exception pipe"));
            var logger = Substitute.For<ILogger>();

            var resultat = new Pipeline<bool>(logger)
                .Configurer(_ => _
                    .Ajouter(sourceDePipeline)
                    .Ajouter(CreerPipe("test1", "test2"))
                    .Ajouter(CreerPipe("test1", true)))
                .Executer();

            resultat.IsFailed.Should().BeTrue();
            resultat.Errors.Should().HaveCount(1);
            var erreurs = resultat.Errors.Select(_ => _.Message);
            erreurs.First().Should().Be("Value cannot be null. (Parameter 'exception pipe')");
        }

        [Test]
        public void Générer_un_log()
        {
            var logger = Substitute.For<ILogger>();
            var pipeline = new Pipeline<IEnumerable<string>>(logger);
            var pipe = CreerPipe(1, new[] { "element1", "element2" });
            pipe.Executer(Arg.Any<int>()).Returns(new[] { "element1", "element2" });

            pipeline
                .Configurer(_ =>
                {
                    return _
                        .Ajouter(CreerPipe(1, 1))
                        .Ajouter(pipe)
                        .AjouterLog(elements =>
                            $"{elements.Length} elements.")
                        .Ajouter(CreerPipe(Array.Empty<string>(), Array.Empty<string>()));
                })
                .Executer();

            logger.Received(1).LogInformation("2 elements.");
        }

        [Test]
        public void AfficherUnMessageDErreurs()
        {
            var logger = Substitute.For<ILogger>();
            var pipeline = new Pipeline<string>(logger);
            var lePipe = CreerPipe(string.Empty, 1);
            lePipe.Executer(Arg.Any<string>()).Throws(new ArgumentNullException("toto"));
            var resultat = pipeline.Configurer(_ =>
                {
                    return _
                        .Ajouter(CreerPipe(string.Empty, string.Empty))
                        .Ajouter(lePipe)
                        .Ajouter(CreerPipe(1, string.Empty))
                        .Ajouter(CreerPipe(string.Empty, string.Empty))
                        .Ajouter(CreerPipe(string.Empty, string.Empty))
                        .AjouterLogDErreur(erreur => $"{erreur}");
                })
                .Executer();

            resultat.IsFailed.Should().BeTrue();
            resultat.Errors.Should().HaveCount(1);
            resultat.Errors.Select(_ => _.Message).Should().BeEquivalentTo(new[]
            {
                "Value cannot be null. (Parameter 'toto')"
            });
            logger.Received(1).LogError("Value cannot be null. (Parameter 'toto')");
        }

        [Test]
        public void AfficherDeuxMessagesDErreurs()
        {
            var logger = Substitute.For<ILogger>();
            var pipeline = new Pipeline<string>(logger);
            var lePipe = CreerPipe(string.Empty, string.Empty);
            lePipe.Executer(Arg.Any<string>()).Throws(new ArgumentNullException("toto"));
            var lePipe2 = CreerPipe(string.Empty, string.Empty);
            lePipe2.Executer(Arg.Any<string>()).Throws(new ArgumentNullException("toto2"));

            var resultat = pipeline.Configurer(_ =>
                {
                    return _
                        .Ajouter(CreerPipe(string.Empty, string.Empty))
                        .Ajouter(lePipe)
                        .Ajouter(CreerPipe(string.Empty, string.Empty))
                        .Ajouter(lePipe2)
                        .Ajouter(CreerPipe(string.Empty, string.Empty));
                })
                .Executer();

            resultat.IsSuccess.Should().BeTrue();
            logger.Received(1).LogError("Value cannot be null. (Parameter 'toto')");
            logger.Received(1).LogError("Value cannot be null. (Parameter 'toto2')");
        }

        private static IPipe<TIn, TOut> CreerPipe<TIn, TOut>(TIn entree, TOut sortie)
        {
            var pipe = Substitute.For<IPipe<TIn, TOut>>();
            pipe.Executer(entree).Returns(sortie);
            return pipe;
        }
    }
}