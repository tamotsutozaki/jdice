import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { NovoUsuario } from './novo-usuario';

describe('NovoUsuario', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [NovoUsuario],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  function preencher(email: string, senha: string, role = 'User') {
    const fixture = TestBed.createComponent(NovoUsuario);
    fixture.detectChanges();

    const form = (fixture.componentInstance as unknown as { form: FormularioNovoUsuario }).form;
    form.setValue({ email, senha, role });

    return fixture;
  }

  function enviar(fixture: ComponentFixture<NovoUsuario>) {
    (fixture.nativeElement as HTMLElement)
      .querySelector('form')
      ?.dispatchEvent(new Event('submit'));
  }

  function texto(fixture: ComponentFixture<NovoUsuario>): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('não chama a API com senha abaixo do mínimo', () => {
    const fixture = preencher('novo@empresa.com', 'curta');

    enviar(fixture);
    fixture.detectChanges();

    // A política é do servidor, mas avisar antes evita uma ida à rede só
    // para receber 400.
    httpMock.expectNone('/api/auth/users');
    expect(texto(fixture)).toContain('entre 12 e 128 caracteres');
  });

  it('não chama a API com e-mail inválido', () => {
    const fixture = preencher('nao-e-email', 'senha-bem-comprida-123');

    enviar(fixture);
    fixture.detectChanges();

    httpMock.expectNone('/api/auth/users');
  });

  it('envia e-mail, senha e perfil escolhidos', async () => {
    const fixture = preencher('novo@empresa.com', 'senha-bem-comprida-123', 'Admin');

    enviar(fixture);

    const requisicao = httpMock.expectOne('/api/auth/users');
    expect(requisicao.request.body).toEqual({
      email: 'novo@empresa.com',
      senha: 'senha-bem-comprida-123',
      role: 'Admin',
    });

    requisicao.flush({ id: 'abc', email: 'novo@empresa.com', role: 'Admin' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Conta criada para novo@empresa.com');
  });

  it('limpa e-mail e senha após criar, mantendo o perfil', async () => {
    const fixture = preencher('novo@empresa.com', 'senha-bem-comprida-123', 'Admin');

    enviar(fixture);
    httpMock
      .expectOne('/api/auth/users')
      .flush({ id: 'abc', email: 'novo@empresa.com', role: 'Admin' });

    await fixture.whenStable();

    const form = (fixture.componentInstance as unknown as { form: FormularioNovoUsuario }).form;

    expect(form.getRawValue()).toEqual({ email: '', senha: '', role: 'Admin' });
  });

  it('avisa quando o e-mail já está cadastrado', async () => {
    const fixture = preencher('repetido@empresa.com', 'senha-bem-comprida-123');

    enviar(fixture);
    httpMock.expectOne('/api/auth/users').flush(null, { status: 409, statusText: 'Conflict' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('Já existe uma conta com esse e-mail.');
  });

  it('avisa quando o servidor recusa por falta de permissão', async () => {
    const fixture = preencher('novo@empresa.com', 'senha-bem-comprida-123');

    enviar(fixture);
    httpMock.expectOne('/api/auth/users').flush(null, { status: 403, statusText: 'Forbidden' });

    await fixture.whenStable();
    fixture.detectChanges();

    expect(texto(fixture)).toContain('não tem permissão');
  });
});

interface FormularioNovoUsuario {
  setValue(valor: { email: string; senha: string; role: string }): void;
  getRawValue(): { email: string; senha: string; role: string };
}
