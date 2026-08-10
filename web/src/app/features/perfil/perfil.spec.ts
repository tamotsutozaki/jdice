import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';

import { AuthService, CurrentUser } from '../../core/auth/auth.service';
import { Perfil } from './perfil';

describe('Perfil', () => {
  let trocarSenha: ReturnType<typeof vi.fn>;

  function configurar(role: CurrentUser['role'] = 'User') {
    trocarSenha = vi.fn().mockReturnValue(of(void 0));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Perfil],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            user: signal<CurrentUser | null>({ id: 'u1', email: 'ana@empresa.com', role }),
            isAdmin: signal(role === 'Admin'),
            trocarSenha,
            logout: vi.fn().mockReturnValue(of(void 0)),
          },
        },
      ],
    });
  }

  function montar() {
    const fixture = TestBed.createComponent(Perfil);
    fixture.detectChanges();
    return fixture;
  }

  function texto(fixture: ComponentFixture<Perfil>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  function preencher(fixture: ComponentFixture<Perfil>, atual: string, nova: string, conf: string) {
    const inputs = (fixture.nativeElement as HTMLElement).querySelectorAll('input');
    const set = (el: HTMLInputElement, v: string) => {
      el.value = v;
      el.dispatchEvent(new Event('input'));
    };
    set(inputs[0] as HTMLInputElement, atual);
    set(inputs[1] as HTMLInputElement, nova);
    set(inputs[2] as HTMLInputElement, conf);
    fixture.detectChanges();
  }

  it('mostra e-mail e perfil de quem está logado', () => {
    configurar('Admin');
    const fixture = montar();

    expect(texto(fixture)).toContain('ana@empresa.com');
    expect(texto(fixture)).toContain('Administrador');
  });

  it('troca a senha e confirma o sucesso', () => {
    configurar();
    const fixture = montar();

    preencher(fixture, 'senha-atual-comprida', 'nova-senha-bem-longa', 'nova-senha-bem-longa');
    (fixture.nativeElement as HTMLElement)
      .querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(trocarSenha).toHaveBeenCalledWith('senha-atual-comprida', 'nova-senha-bem-longa');
    expect(texto(fixture)).toContain('Senha alterada');
  });

  it('não envia quando a confirmação não bate', () => {
    configurar();
    const fixture = montar();

    preencher(fixture, 'senha-atual-comprida', 'nova-senha-bem-longa', 'diferente-comprida');
    (fixture.nativeElement as HTMLElement)
      .querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(trocarSenha).not.toHaveBeenCalled();
    expect(texto(fixture)).toContain('As senhas não conferem');
  });

  it('mostra a mensagem do servidor quando a senha atual está errada', () => {
    configurar();
    trocarSenha.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 400,
            error: { detail: 'A senha atual está incorreta.' },
          }),
      ),
    );
    const fixture = montar();

    preencher(fixture, 'errada-mas-comprida', 'nova-senha-bem-longa', 'nova-senha-bem-longa');
    (fixture.nativeElement as HTMLElement)
      .querySelector('form')!
      .dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(texto(fixture)).toContain('A senha atual está incorreta.');
  });
});
