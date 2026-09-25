import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  createClassroom,
  deleteClassroom,
  errorMessage,
  getClassrooms,
  renameClassroom,
} from "../services/api";
import ClassroomMembersForm from "../components/ClassroomMembersForm";
import ClassroomNameForm from "../components/ClassroomNameForm";
import LogoLoading from "../components/LogoLoading";

export default function ClassroomsPage() {
  const navigate = useNavigate();
  const [classrooms, setClassrooms] = useState(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [attempt, reload] = useState(0);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState(null);
  useEffect(() => {
    let active = true;
    getClassrooms()
      .then((data) => {
        if (active) setClassrooms(data);
      })
      .catch((e) => {
        if (active) setError(errorMessage(e));
      });
    return () => {
      active = false;
    };
  }, [attempt]);
  async function action(operation) {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await operation();
      setEditing(null);
      reload((value) => value + 1);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section>
      <div className="d-flex flex-wrap justify-content-between gap-3 mb-3">
        <h1 className="h2">Minhas turmas</h1>
        {!creating && (
          <button
            className="btn btn-primary"
            disabled={busy}
            onClick={() => {
              setCreating(true);
              setEditing(null);
              setError("");
            }}
          >
            Nova turma
          </button>
        )}
      </div>
      <p className="text-body-secondary">
        Organize perfis de Minhas análises e acompanhe evidências agregadas.
        Abrir turmas utiliza dados salvos, sem consultar o GitHub.
      </p>
      {error && (
        <div className="alert alert-danger" role="alert">
          {error}{" "}
          <button
            className="btn btn-sm btn-outline-danger"
            disabled={busy}
            onClick={() => {
              setError("");
              reload((value) => value + 1);
            }}
          >
            Recarregar
          </button>
        </div>
      )}

      {busy && (
        <LogoLoading overlay text="Processando alteração na turma…" size={72} />
      )}
      {creating ? (
        <ClassroomMembersForm
          creating
          busy={busy}
          onCancel={() => setCreating(false)}
          onSubmit={(name, ids) =>
            action(async () => {
              const created = await createClassroom(name, ids);
              navigate(`/turmas/${created.id}`);
            })
          }
        />
      ) : (
        <>
          {editing && (
            <ClassroomNameForm
              key={editing.id}
              initialName={editing.name}
              busy={busy}
              onCancel={() => setEditing(null)}
              onSave={(name) => action(() => renameClassroom(editing.id, name))}
            />
          )}
          {!classrooms && !error && (
            <LogoLoading text="Carregando turmas…" size={72} />
          )}
          {classrooms?.length === 0 && (
            <div className="card">
              <div className="card-body p-4">
                <p>Você ainda não criou nenhuma turma.</p>
                <button
                  className="btn btn-primary"
                  onClick={() => setCreating(true)}
                >
                  Criar minha primeira turma
                </button>
              </div>
            </div>
          )}
          <div className="row g-3">
            {classrooms?.map((classroom) => (
              <div className="col-12 col-lg-6" key={classroom.id}>
                <article className="card h-100">
                  <div className="card-body">
                    <h2 className="h4 text-break">{classroom.name}</h2>
                    <p>{classroom.memberCount} aluno(s)</p>
                    <p className="small text-body-secondary">
                      Última atualização da turma:{" "}
                      {new Date(classroom.updatedAt).toLocaleString("pt-BR")}
                    </p>
                    <div className="d-flex flex-wrap gap-2 mb-3">
                      {classroom.technologies.map((item) => (
                        <span
                          className="badge text-bg-secondary text-wrap"
                          key={item.label}
                        >
                          {item.label} · {item.count} aluno(s)
                        </span>
                      ))}
                    </div>
                    <div className="d-flex flex-wrap gap-2">
                      <Link
                        className="btn btn-primary"
                        to={`/turmas/${classroom.id}`}
                      >
                        Abrir turma
                      </Link>
                      <button
                        className="btn btn-outline-secondary"
                        disabled={busy}
                        onClick={() => setEditing(classroom)}
                      >
                        Editar nome
                      </button>
                      <button
                        className="btn btn-outline-danger"
                        disabled={busy}
                        onClick={() => {
                          if (
                            window.confirm(
                              `Excluir a turma “${classroom.name}”? Os perfis, análises e sua lista pessoal serão preservados.`,
                            )
                          )
                            action(() => deleteClassroom(classroom.id));
                        }}
                      >
                        Excluir
                      </button>
                    </div>
                  </div>
                </article>
              </div>
            ))}
          </div>
        </>
      )}
    </section>
  );
}
