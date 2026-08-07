import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { Login } from './login';

describe('Login', () => {
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  function preencher(email: string, senha: string) {
    const fixture = TestBed.createComponent(Login);
    fixture.detectChanges();

    const form = (fixture.componentInstance as unknown as { form: FormularioLogin }).form;
    form.setValue({ email, senha });

    return fixture;
  }

  it('não chama a API quando o formulário está inválido', () => {
    const fixture = preencher('nao-e-email', '');

    (fixture.nativeElement as HTMLElement)
      .querySelector('form')
      ?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    httpMock.expectNone('/api/auth/login');
  });

  it('envia as credenciais e navega para o destino após entrar', async () => {
    const navigateByUrl = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
    const fixture = preencher('pedro@empresa.com', 'senha-bem-comprida-123');

    (fixture.nativeElement as HTMLElement)
      .querySelector('form')
      ?.dispatchEvent(new Event('submit'));

    const login = httpMock.expectOne('/api/auth/login');
    expect(login.request.body).toEqual({
      email: 'pedro@empresa.com',
      senha: 'senha-bem-comprida-123',
    });
    login.flush(null);

    httpMock.expectOne('/api/auth/me').flush({
      id: '019fdd3f-8c71-7534-a61d-f1e430fe4d80',
      email: 'pedro@empresa.com',
      role: 'User',
    });

    await fixture.whenStable();

    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('explica o bloqueio por excesso de tentativas', async () => {
    const fixture = preencher('pedro@empresa.com', 'senha-bem-comprida-123');

    (fixture.nativeElement as HTMLElement)
      .querySelector('form')
      ?.dispatchEvent(new Event('submit'));

    httpMock
      .expectOne('/api/auth/login')
      .flush(null, { status: 429, statusText: 'Too Many Requests' });

    await fixture.whenStable();
    fixture.detectChanges();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    // Sem dizer que é limite de tentativas, a pessoa insiste achando que
    // errou a senha — e cada insistência renova o bloqueio.
    expect(texto).toContain('Muitas tentativas seguidas');
    expect(texto).not.toContain('E-mail ou senha incorretos');
  });

  it('mostra mensagem genérica quando as credenciais são recusadas', async () => {
    const fixture = preencher('pedro@empresa.com', 'senha-errada-123456');

    (fixture.nativeElement as HTMLElement)
      .querySelector('form')
      ?.dispatchEvent(new Event('submit'));

    httpMock.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    await fixture.whenStable();
    fixture.detectChanges();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';

    // Não pode revelar se o e-mail existe: a mensagem cobre os dois casos.
    expect(texto).toContain('E-mail ou senha incorretos.');
  });
});

interface FormularioLogin {
  setValue(valor: { email: string; senha: string }): void;
}
