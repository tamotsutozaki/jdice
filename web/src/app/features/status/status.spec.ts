import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';

import { Status } from './status';

describe('Status', () => {
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Status],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('mostra "no ar" quando os dois health checks respondem Healthy', async () => {
    const fixture = TestBed.createComponent(Status);
    fixture.detectChanges();

    httpMock.expectOne('/health/live').flush('Healthy');
    httpMock.expectOne('/health/ready').flush('Healthy');

    await fixture.whenStable();
    fixture.detectChanges();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('no ar');
    expect(texto).not.toContain('indisponível');
  });

  it('mostra "indisponível" quando o health check do banco falha', async () => {
    const fixture = TestBed.createComponent(Status);
    fixture.detectChanges();

    httpMock.expectOne('/health/live').flush('Healthy');
    httpMock
      .expectOne('/health/ready')
      .flush('Unhealthy', { status: 503, statusText: 'Service Unavailable' });

    await fixture.whenStable();
    fixture.detectChanges();

    const texto = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(texto).toContain('indisponível');
  });
});
